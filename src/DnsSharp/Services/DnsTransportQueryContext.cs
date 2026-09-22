using DnsSharp.Models;

namespace DnsSharp.Services;

/// <summary>
/// Transport execution context for a single DNS request.
/// </summary>
public sealed class DnsTransportQueryContext
{
    /// <summary>
    /// Gets target server endpoint as host:port.
    /// </summary>
    public required string Server { get; init; }

    /// <summary>
    /// Gets encoded DNS request bytes.
    /// </summary>
    public required byte[] RequestBytes { get; init; }

    /// <summary>
    /// Gets request message metadata.
    /// </summary>
    public required DnsMessage RequestMessage { get; init; }

    /// <summary>
    /// Gets timeout to apply to this transport call.
    /// </summary>
    public required TimeSpan Timeout { get; init; }
}
