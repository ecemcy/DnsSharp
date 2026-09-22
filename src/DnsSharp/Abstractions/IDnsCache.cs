using DnsSharp.Models;

namespace DnsSharp.Abstractions;

/// <summary>
/// Defines cache operations for DNS responses.
/// </summary>
public interface IDnsCache
{
    /// <summary>
    /// Attempts to get a cached response for the query.
    /// </summary>
    /// <param name="name">Query name.</param>
    /// <param name="recordType">Query record type.</param>
    /// <param name="response">Cached response if found.</param>
    /// <returns>True when cache contains a valid entry.</returns>
    bool TryGet(string name, RecordType recordType, out DnsMessage? response);

    /// <summary>
    /// Stores a response with positive TTL semantics.
    /// </summary>
    /// <param name="name">Query name.</param>
    /// <param name="recordType">Query record type.</param>
    /// <param name="response">Response to cache.</param>
    void Set(string name, RecordType recordType, DnsMessage response);
}
