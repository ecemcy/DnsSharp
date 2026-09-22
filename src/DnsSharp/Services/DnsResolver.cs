using DnsSharp.Abstractions;
using DnsSharp.Models;
using DnsSharp.Options;
using DnsSharp.Resilience;
using DnsSharp.Telemetry;
using DnsSharp.Wire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DnsSharp.Services;

/// <summary>
/// Default DNS resolver implementation.
/// </summary>
public sealed class DnsResolver : IDnsResolver
{
    private readonly IOptionsMonitor<DnsOptions> _options;
    private readonly IReadOnlyDictionary<string, IDnsTransport> _transports;
    private readonly IDnsCache _cache;
    private readonly IDnsWireCodec _codec;
    private readonly ILogger<DnsResolver> _logger;
    private readonly IDnsResilienceStrategy _resilience;
    private readonly DnsTelemetry _telemetry;

    /// <summary>
    /// Creates a new <see cref="DnsResolver"/> instance.
    /// </summary>
    public DnsResolver(
        IOptionsMonitor<DnsOptions> options,
        IEnumerable<IDnsTransport> transports,
        IDnsCache cache,
        IDnsWireCodec codec,
        ILogger<DnsResolver> logger,
        IDnsResilienceStrategy resilience,
        DnsTelemetry telemetry)
    {
        _options = options;
        _transports = transports
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        _cache = cache;
        _codec = codec;
        _logger = logger;
        _resilience = resilience;
        _telemetry = telemetry;
    }

    /// <inheritdoc />
    public async Task<DnsMessage> QueryAsync(string name, RecordType recordType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("DNS name is required.", nameof(name));
        }

        if (_cache.TryGet(name, recordType, out var cached) && cached is not null)
        {
            return cached;
        }

        return await _resilience.ExecuteAsync(
            async token =>
            {
                var response = await ExecuteQueryInternalAsync(name, recordType, token).ConfigureAwait(false);
                _cache.Set(name, recordType, response);
                return response;
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<DnsMessage> ExecuteQueryInternalAsync(string name, RecordType recordType, CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        var request = BuildQuery(name, recordType, options.EnableEdns, options.EdnsUdpPayloadSize);
        var bytes = _codec.Encode(request);
        var timeout = TimeSpan.FromMilliseconds(Math.Max(100, options.QueryTimeoutMs));
        var order = options.TransportOrder.Count > 0 ? options.TransportOrder : new List<string> { "udp", "tcp" };

        Exception? lastException = null;

        foreach (var transportName in order)
        {
            if (!_transports.TryGetValue(transportName, out var transport))
            {
                _logger.LogDebug("Skipping transport {Transport}: no registered implementation.", transportName);
                continue;
            }

            var targets = ResolveTargets(transportName, options);
            foreach (var target in targets)
            {
                try
                {
                    using var activity = _telemetry.StartActivity("dns.resolve");
                    activity?.SetTag("dns.transport", transport.Name);
                    activity?.SetTag("dns.server", target);
                    activity?.SetTag("dns.question.name", name);
                    activity?.SetTag("dns.question.type", recordType.ToString());

                    var context = new DnsTransportQueryContext
                    {
                        Server = target,
                        RequestBytes = bytes,
                        RequestMessage = request,
                        Timeout = timeout
                    };

                    var payload = await transport.QueryAsync(context, cancellationToken).ConfigureAwait(false);
                    var response = _codec.Decode(payload);

                    if (response.Id != request.Id)
                    {
                        throw new InvalidDataException("DNS response ID mismatch.");
                    }

                    if (response.Truncated &&
                        options.EnableTcpFallback &&
                        transport.Name.Equals("udp", StringComparison.OrdinalIgnoreCase))
                    {
                        if (_transports.TryGetValue("tcp", out var tcpTransport))
                        {
                            var tcpTarget = target;
                            if (transport.Name.Equals("doh", StringComparison.OrdinalIgnoreCase))
                            {
                                tcpTarget = options.Servers.FirstOrDefault() ?? "1.1.1.1:53";
                            }

                            var tcpContext = new DnsTransportQueryContext
                            {
                                Server = tcpTarget,
                                RequestBytes = bytes,
                                RequestMessage = request,
                                Timeout = timeout
                            };

                            var tcpPayload = await tcpTransport.QueryAsync(tcpContext, cancellationToken).ConfigureAwait(false);
                            var tcpResponse = _codec.Decode(tcpPayload);
                            if (tcpResponse.Id != request.Id)
                            {
                                throw new InvalidDataException("DNS response ID mismatch on TCP fallback.");
                            }

                            return tcpResponse;
                        }
                    }

                    return response;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.LogWarning(ex, "DNS query failed via transport {Transport} against target {Target}", transport.Name, target);
                }
            }
        }

        throw new InvalidOperationException("All DNS query attempts failed.", lastException);
    }

    private static IEnumerable<string> ResolveTargets(string transportName, DnsOptions options)
    {
        if (transportName.Equals("doh", StringComparison.OrdinalIgnoreCase))
        {
            // For DoH, endpoint selection is delegated to configured DnsOptions.DohEndpoint.
            return new[] { options.DohEndpoint ?? string.Empty };
        }

        return options.Servers.Count > 0 ? options.Servers : new[] { "1.1.1.1:53", "8.8.8.8:53" };
    }

    private static DnsMessage BuildQuery(string name, RecordType recordType, bool enableEdns, ushort udpPayloadSize)
    {
        var message = new DnsMessage
        {
            Id = (ushort)Random.Shared.Next(1, ushort.MaxValue),
            IsResponse = false,
            RecursionDesired = true
        };

        message.Questions.Add(new DnsQuestion { Name = name, Type = recordType, Class = DnsClass.IN });

        if (enableEdns)
        {
            message.Additionals.Add(new DnsRecord
            {
                Name = ".",
                Type = RecordType.OPT,
                Class = (DnsClass)udpPayloadSize,
                Ttl = 0,
                RawData = Array.Empty<byte>(),
                Data = null
            });
        }

        return message;
    }
}
