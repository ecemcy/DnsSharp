namespace DnsSharp.Telemetry;

/// <summary>
/// Names used by DnsSharp OpenTelemetry hooks.
/// </summary>
public static class DnsTelemetryConventions
{
    /// <summary>
    /// Activity source name emitted by DnsSharp.
    /// </summary>
    public const string ActivitySourceName = "DnsSharp";

    /// <summary>
    /// Meter name emitted by DnsSharp.
    /// </summary>
    public const string MeterName = "DnsSharp";
}
