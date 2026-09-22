using System.Diagnostics;
using System.Net.Http.Headers;
using DnsSharp.Abstractions;
using DnsSharp.Options;
using DnsSharp.Services;
using DnsSharp.Telemetry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DnsSharp.Transports;

/// <summary>
/// DNS-over-HTTPS transport implementation.
/// </summary>
public sealed class DohDnsTransport : IDnsTransport
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<DnsOptions> _options;
    private readonly ILogger<DohDnsTransport> _logger;
    private readonly DnsTelemetry _telemetry;

    /// <summary>
    /// Creates a new <see cref="DohDnsTransport"/>.
    /// </summary>
    public DohDnsTransport(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<DnsOptions> options,
        ILogger<DohDnsTransport> logger,
        DnsTelemetry telemetry)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
        _telemetry = telemetry;
    }

    /// <inheritdoc />
    public string Name => "doh";

    /// <inheritdoc />
    public async Task<byte[]> QueryAsync(DnsTransportQueryContext context, CancellationToken cancellationToken = default)
    {
        using var activity = _telemetry.StartActivity("dns.transport.doh");
        var started = Stopwatch.GetTimestamp();
        var options = _options.CurrentValue;

        if (string.IsNullOrWhiteSpace(options.DohEndpoint))
        {
            throw new InvalidOperationException("DoH endpoint is not configured. Set DnsOptions.DohEndpoint.");
        }

        try
        {
            using var timeoutCts = new CancellationTokenSource(context.Timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

            var client = _httpClientFactory.CreateClient("DnsSharp.DoH");
            using var request = CreateRequest(options.DohEndpoint, options.DohUseGet, context.RequestBytes);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadAsByteArrayAsync(linkedCts.Token).ConfigureAwait(false);
            var duration = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            _telemetry.RecordSuccess(Name, duration);
            return payload;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var duration = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            _telemetry.RecordFailure(Name, duration);
            _logger.LogDebug(ex, "DoH query failed for endpoint {Endpoint}", options.DohEndpoint);
            throw;
        }
    }

    private static HttpRequestMessage CreateRequest(string endpoint, bool useGet, byte[] payload)
    {
        if (useGet)
        {
            var encoded = Convert.ToBase64String(payload).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            var separator = endpoint.Contains('?') ? "&" : "?";
            var url = $"{endpoint}{separator}dns={encoded}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/dns-message"));
            return request;
        }

        var post = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new ByteArrayContent(payload)
        };

        post.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/dns-message"));
        post.Content.Headers.ContentType = new MediaTypeHeaderValue("application/dns-message");
        return post;
    }
}
