namespace DnsSharp.Models;

/// <summary>
/// Represents a DNS question section entry.
/// </summary>
public sealed class DnsQuestion
{
    /// <summary>
    /// Gets or sets the domain name being queried.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the record type being queried.
    /// </summary>
    public RecordType Type { get; init; } = RecordType.A;

    /// <summary>
    /// Gets or sets the DNS class.
    /// </summary>
    public DnsClass Class { get; init; } = DnsClass.IN;
}
