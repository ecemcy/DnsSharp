using DnsSharp.Models;

namespace DnsSharp.Abstractions;

/// <summary>
/// Resolves DNS queries using configured transport(s), caching, and resilience behavior.
/// </summary>
public interface IDnsResolver
{
    /// <summary>
    /// Queries DNS for the specified domain and record type.
    /// </summary>
    /// <param name="name">Domain name to resolve.</param>
    /// <param name="recordType">Record type to query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parsed DNS response.</returns>
    Task<DnsMessage> QueryAsync(string name, RecordType recordType, CancellationToken cancellationToken = default);
}
