namespace DnsSharp.Options;

/// <summary>
/// Resolver options for DNS operations.
/// </summary>
public sealed class DnsOptions
{
    /// <summary>
    /// Gets server endpoints in host:port format.
    /// </summary>
    public List<string> Servers { get; } = new() { "1.1.1.1:53", "8.8.8.8:53" };

    /// <summary>
    /// Gets preferred transport names in execution order.
    /// </summary>
    public List<string> TransportOrder { get; } = new() { "udp", "tcp" };

    /// <summary>
    /// Gets or sets query timeout in milliseconds.
    /// </summary>
    public int QueryTimeoutMs { get; set; } = 3000;

    /// <summary>
    /// Gets or sets a value indicating whether TCP fallback is enabled when UDP is truncated.
    /// </summary>
    public bool EnableTcpFallback { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether cache is enabled.
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Gets or sets default positive TTL in seconds when records provide no TTL.
    /// </summary>
    public int DefaultTtlSeconds { get; set; } = 300;

    /// <summary>
    /// Gets or sets negative cache TTL in seconds.
    /// </summary>
    public int NegativeCacheTtlSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets the UDP payload size announced via EDNS.
    /// </summary>
    public ushort EdnsUdpPayloadSize { get; set; } = 1232;

    /// <summary>
    /// Gets or sets a value indicating whether to include OPT in outgoing queries.
    /// </summary>
    public bool EnableEdns { get; set; } = true;

    /// <summary>
    /// Gets or sets the DNS-over-HTTPS endpoint URI.
    /// </summary>
    public string? DohEndpoint { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether DoH requests use GET method.
    /// </summary>
    public bool DohUseGet { get; set; }
}
