namespace KeyloopScheduler.Domain.Abstractions;

/// <summary>
/// Transaction boundary abstraction. The booking engine depends on this so that
/// the concrete isolation strategy (serializable transaction, row locks) stays in
/// the infrastructure layer.
/// </summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="operation"/> inside a transaction whose isolation
    /// level prevents concurrent double-booking, then commits on success and
    /// rolls back on failure.
    /// </summary>
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
