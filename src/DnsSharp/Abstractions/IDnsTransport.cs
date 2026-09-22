using DnsSharp.Models;
using DnsSharp.Services;

namespace DnsSharp.Abstractions;

/// <summary>
/// Represents a DNS transport implementation (UDP, TCP, DoH, etc.).
/// </summary>
public interface IDnsTransport
{
    /// <summary>
    /// Gets the transport name used for deterministic selection.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Executes a DNS query through this transport.
    /// </summary>
    /// <param name="context">Transport query context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Raw DNS response payload bytes.</returns>
    Task<byte[]> QueryAsync(DnsTransportQueryContext context, CancellationToken cancellationToken = default);
}
