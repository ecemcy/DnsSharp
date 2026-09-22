using System.Diagnostics;
using System.Net.Sockets;
using DnsSharp.Abstractions;
using DnsSharp.Services;
using DnsSharp.Telemetry;
using Microsoft.Extensions.Logging;

namespace DnsSharp.Transports;

/// <summary>
/// DNS transport over UDP.
/// </summary>
public sealed class UdpDnsTransport : IDnsTransport
{
    private readonly ILogger<UdpDnsTransport> _logger;
    private readonly DnsTelemetry _telemetry;

    /// <summary>
    /// Creates a new <see cref="UdpDnsTransport"/>.
    /// </summary>
    public UdpDnsTransport(ILogger<UdpDnsTransport> logger, DnsTelemetry telemetry)
    {
        _logger = logger;
        _telemetry = telemetry;
    }

    /// <inheritdoc />
    public string Name => "udp";

    /// <inheritdoc />
    public async Task<byte[]> QueryAsync(DnsTransportQueryContext context, CancellationToken cancellationToken = default)
    {
        using var activity = _telemetry.StartActivity("dns.transport.udp");
        var started = Stopwatch.GetTimestamp();

        try
        {
            var (host, port) = EndpointParser.Parse(context.Server);
            using var udp = new UdpClient();

            using var timeoutCts = new CancellationTokenSource(context.Timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

            await udp.SendAsync(context.RequestBytes, context.RequestBytes.Length, host, port, linkedCts.Token).ConfigureAwait(false);
            var result = await udp.ReceiveAsync(linkedCts.Token).ConfigureAwait(false);

            var duration = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            _telemetry.RecordSuccess(Name, duration);
            return result.Buffer;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var duration = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            _telemetry.RecordFailure(Name, duration);
            _logger.LogDebug(ex, "UDP DNS query failed for server {Server}", context.Server);
            throw;
        }
    }
}
