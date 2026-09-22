using DnsSharp.Models;
using DnsSharp.Options;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace DnsSharp.Resilience;

internal sealed class PollyDnsResilienceStrategy : IDnsResilienceStrategy
{
    private readonly ResiliencePipeline<DnsMessage> _pipeline;

    public PollyDnsResilienceStrategy(IOptions<DnsPollyOptions> options)
    {
        var settings = options.Value;

        var retryOptions = new RetryStrategyOptions<DnsMessage>
        {
            ShouldHandle = new PredicateBuilder<DnsMessage>().Handle<Exception>(),
            MaxRetryAttempts = Math.Max(0, settings.RetryCount),
            Delay = TimeSpan.FromMilliseconds(Math.Max(1, settings.RetryBaseDelayMs)),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true
        };

        var breakerOptions = new CircuitBreakerStrategyOptions<DnsMessage>
        {
            ShouldHandle = new PredicateBuilder<DnsMessage>().Handle<Exception>(),
            FailureRatio = 0.5,
            SamplingDuration = TimeSpan.FromSeconds(30),
            MinimumThroughput = Math.Max(2, settings.CircuitBreakerFailures),
            BreakDuration = TimeSpan.FromSeconds(Math.Max(1, settings.CircuitBreakerDurationSeconds))
        };

        _pipeline = new ResiliencePipelineBuilder<DnsMessage>()
            .AddRetry(retryOptions)
            .AddCircuitBreaker(breakerOptions)
            .Build();
    }

    public async Task<DnsMessage> ExecuteAsync(Func<CancellationToken, Task<DnsMessage>> action, CancellationToken cancellationToken)
    {
        return await _pipeline.ExecuteAsync(
            async token => await action(token).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
    }
}
