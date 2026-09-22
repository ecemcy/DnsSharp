using DnsSharp.Models;

namespace DnsSharp.Resilience;

internal sealed class NoOpDnsResilienceStrategy : IDnsResilienceStrategy
{
    public Task<DnsMessage> ExecuteAsync(Func<CancellationToken, Task<DnsMessage>> action, CancellationToken cancellationToken)
        => action(cancellationToken);
}
