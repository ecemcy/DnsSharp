using DnsSharp.Models;

namespace DnsSharp.Resilience;

/// <summary>
/// Executes DNS operations under an optional resilience policy.
/// </summary>
public interface IDnsResilienceStrategy
{
    /// <summary>
    /// Executes the provided DNS operation.
    /// </summary>
    /// <param name="action">Operation delegate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>DNS message from the operation.</returns>
    Task<DnsMessage> ExecuteAsync(Func<CancellationToken, Task<DnsMessage>> action, CancellationToken cancellationToken);
}
