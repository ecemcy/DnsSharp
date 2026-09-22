namespace DnsSharp.Models;

/// <summary>
/// Represents a DNS resource record.
/// </summary>
public sealed class DnsRecord
{
    /// <summary>
    /// Gets or sets the owner name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the record type.
    /// </summary>
    public RecordType Type { get; init; }

    /// <summary>
    /// Gets or sets the DNS class.
    /// </summary>
    public DnsClass Class { get; init; }

    /// <summary>
    /// Gets or sets the record TTL.
    /// </summary>
    public uint Ttl { get; init; }

    /// <summary>
    /// Gets or sets raw RDATA bytes exactly as received.
    /// </summary>
    public required byte[] RawData { get; init; }

    /// <summary>
    /// Gets or sets parsed data when supported for this record type.
    /// </summary>
    public object? Data { get; init; }
}
