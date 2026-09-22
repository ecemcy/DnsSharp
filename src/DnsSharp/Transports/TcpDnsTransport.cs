using System.Buffers.Binary;
using System.Diagnostics;
using System.Net.Sockets;
using DnsSharp.Abstractions;
using DnsSharp.Services;
using DnsSharp.Telemetry;
using Microsoft.Extensions.Logging;

namespace DnsSharp.Transports;

/// <summary>
/// DNS transport over TCP.
/// </summary>
public sealed class TcpDnsTransport : IDnsTransport
{
    private readonly ILogger<TcpDnsTransport> _logger;
    private readonly DnsTelemetry _telemetry;

    /// <summary>
    /// Creates a new <see cref="TcpDnsTransport"/>.
    /// </summary>
    public TcpDnsTransport(ILogger<TcpDnsTransport> logger, DnsTelemetry telemetry)
    {
        _logger = logger;
        _telemetry = telemetry;
    }

    /// <inheritdoc />
    public string Name => "tcp";

    /// <inheritdoc />
    public async Task<byte[]> QueryAsync(DnsTransportQueryContext context, CancellationToken cancellationToken = default)
    {
        using var activity = _telemetry.StartActivity("dns.transport.tcp");
        var started = Stopwatch.GetTimestamp();

        try
        {
            var (host, port) = EndpointParser.Parse(context.Server);
            using var timeoutCts = new CancellationTokenSource(context.Timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

            using var client = new TcpClient();
            await client.ConnectAsync(host, port, linkedCts.Token).ConfigureAwait(false);

            await using var stream = client.GetStream();

            // TCP DNS prepends a 2-byte network-order length before the DNS payload.
            var prefixed = new byte[context.RequestBytes.Length + 2];
            BinaryPrimitives.WriteUInt16BigEndian(prefixed, checked((ushort)context.RequestBytes.Length));
            Buffer.BlockCopy(context.RequestBytes, 0, prefixed, 2, context.RequestBytes.Length);
            await stream.WriteAsync(prefixed, linkedCts.Token).ConfigureAwait(false);

            var lengthBuffer = new byte[2];
            await ReadExactAsync(stream, lengthBuffer, linkedCts.Token).ConfigureAwait(false);
            var length = BinaryPrimitives.ReadUInt16BigEndian(lengthBuffer);

            var payload = new byte[length];
            await ReadExactAsync(stream, payload, linkedCts.Token).ConfigureAwait(false);

            var duration = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            _telemetry.RecordSuccess(Name, duration);
            return payload;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var duration = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            _telemetry.RecordFailure(Name, duration);
            _logger.LogDebug(ex, "TCP DNS query failed for server {Server}", context.Server);
            throw;
        }
    }

    private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(read), cancellationToken).ConfigureAwait(false);
            if (bytesRead == 0)
            {
                throw new IOException("Unexpected end of DNS TCP stream.");
            }

            read += bytesRead;
        }
    }
}
