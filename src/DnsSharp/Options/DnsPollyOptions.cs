namespace DnsSharp.Options;

/// <summary>
/// Optional Polly resilience settings.
/// </summary>
public sealed class DnsPollyOptions
{
    /// <summary>
    /// Gets or sets retry attempt count.
    /// </summary>
    public int RetryCount { get; set; } = 2;

    /// <summary>
    /// Gets or sets base retry delay in milliseconds.
    /// </summary>
    public int RetryBaseDelayMs { get; set; } = 150;

    /// <summary>
    /// Gets or sets handled consecutive failure count before opening circuit.
    /// </summary>
    public int CircuitBreakerFailures { get; set; } = 8;

    /// <summary>
    /// Gets or sets circuit break duration in seconds.
    /// </summary>
    public int CircuitBreakerDurationSeconds { get; set; } = 20;
}
