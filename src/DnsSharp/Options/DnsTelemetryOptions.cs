namespace DnsSharp.Options;

/// <summary>
/// Telemetry options for resolver and transports.
/// </summary>
public sealed class DnsTelemetryOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether activities are emitted.
    /// </summary>
    public bool EnableTracing { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether metrics are emitted.
    /// </summary>
    public bool EnableMetrics { get; set; } = true;
}
