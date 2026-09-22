using System.Diagnostics;
using System.Diagnostics.Metrics;
using DnsSharp.Options;
using Microsoft.Extensions.Options;

namespace DnsSharp.Telemetry;

/// <summary>
/// Emits tracing and metric hooks for DnsSharp operations.
/// </summary>
public sealed class DnsTelemetry
{
    private static readonly ActivitySource Source = new(DnsTelemetryConventions.ActivitySourceName);
    private static readonly Meter Meter = new(DnsTelemetryConventions.MeterName);

    private readonly DnsTelemetryOptions _options;
    private readonly Counter<long> _queryCounter = Meter.CreateCounter<long>("dnssharp.queries");
    private readonly Counter<long> _errorCounter = Meter.CreateCounter<long>("dnssharp.query.errors");
    private readonly Histogram<double> _durationHistogram = Meter.CreateHistogram<double>("dnssharp.query.duration.ms");

    /// <summary>
    /// Creates a new <see cref="DnsTelemetry"/> instance.
    /// </summary>
    public DnsTelemetry(IOptions<DnsTelemetryOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// Starts a DNS operation activity when tracing is enabled.
    /// </summary>
    public Activity? StartActivity(string name) => _options.EnableTracing ? Source.StartActivity(name, ActivityKind.Client) : null;

    /// <summary>
    /// Records a successful DNS query metric set.
    /// </summary>
    public void RecordSuccess(string transport, double durationMs)
    {
        if (_options.EnableMetrics)
        {
            _queryCounter.Add(1, new KeyValuePair<string, object?>("transport", transport), new KeyValuePair<string, object?>("status", "success"));
            _durationHistogram.Record(durationMs, new KeyValuePair<string, object?>("transport", transport));
        }
    }

    /// <summary>
    /// Records a failed DNS query metric set.
    /// </summary>
    public void RecordFailure(string transport, double durationMs)
    {
        if (_options.EnableMetrics)
        {
            _queryCounter.Add(1, new KeyValuePair<string, object?>("transport", transport), new KeyValuePair<string, object?>("status", "failure"));
            _errorCounter.Add(1, new KeyValuePair<string, object?>("transport", transport));
            _durationHistogram.Record(durationMs, new KeyValuePair<string, object?>("transport", transport));
        }
    }
}
