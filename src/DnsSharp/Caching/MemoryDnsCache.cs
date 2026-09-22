using DnsSharp.Abstractions;
using DnsSharp.Models;
using DnsSharp.Models.Records;
using DnsSharp.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace DnsSharp.Caching;

/// <summary>
/// In-memory TTL-aware DNS cache with negative caching.
/// </summary>
public sealed class MemoryDnsCache : IDnsCache
{
    private readonly IMemoryCache _cache;
    private readonly DnsOptions _options;

    /// <summary>
    /// Creates a new <see cref="MemoryDnsCache"/> instance.
    /// </summary>
    public MemoryDnsCache(IMemoryCache cache, IOptions<DnsOptions> options)
    {
        _cache = cache;
        _options = options.Value;
    }

    /// <inheritdoc />
    public bool TryGet(string name, RecordType recordType, out DnsMessage? response)
    {
        response = null;
        if (!_options.EnableCaching)
        {
            return false;
        }

        return _cache.TryGetValue(GetKey(name, recordType), out response);
    }

    /// <inheritdoc />
    public void Set(string name, RecordType recordType, DnsMessage response)
    {
        if (!_options.EnableCaching)
        {
            return;
        }

        var ttl = ComputeTtlSeconds(response);
        if (ttl <= 0)
        {
            return;
        }

        _cache.Set(GetKey(name, recordType), response, TimeSpan.FromSeconds(ttl));
    }

    private int ComputeTtlSeconds(DnsMessage response)
    {
        if (response.ResponseCode == ResponseCode.NxDomain || (response.ResponseCode == ResponseCode.NoError && response.Answers.Count == 0))
        {
            // RFC 2308 negative cache fallback using SOA minimum when available.
            var soa = response.Authorities
                .Where(x => x.Type == RecordType.SOA)
                .Select(x => x.Data as SoaRecordData)
                .FirstOrDefault(x => x is not null);

            if (soa is not null)
            {
                var soaTtl = Math.Min((int)soa.Minimum, int.MaxValue);
                return soaTtl > 0 ? soaTtl : _options.NegativeCacheTtlSeconds;
            }

            return _options.NegativeCacheTtlSeconds;
        }

        var ttls = response.Answers
            .Select(x => x.Ttl)
            .Where(x => x > 0)
            .Select(x => Math.Min((int)x, int.MaxValue))
            .ToArray();

        if (ttls.Length == 0)
        {
            return _options.DefaultTtlSeconds;
        }

        return ttls.Min();
    }

    private static string GetKey(string name, RecordType recordType)
        => $"{name.Trim().TrimEnd('.').ToLowerInvariant()}|{recordType}";
}
