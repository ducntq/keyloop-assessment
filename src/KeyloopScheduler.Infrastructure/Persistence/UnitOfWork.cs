using System.Data;
using KeyloopScheduler.Domain.Abstractions;
using KeyloopScheduler.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace KeyloopScheduler.Infrastructure.Persistence;

internal sealed class UnitOfWork : IUnitOfWork
{
    private readonly SchedulerDbContext _context;
    private readonly ILogger<UnitOfWork> _logger;

    public UnitOfWork(SchedulerDbContext context, ILogger<UnitOfWork> logger)
    {
        _context = context;
        _logger = logger;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            try
            {
                var result = await operation(cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch (Exception ex) when (IsContention(ex))
            {
                await SafeRollbackAsync(transaction, cancellationToken);
                _logger.LogWarning(
                    ex,
                    "Booking contention detected; transaction rolled back. SqlState={SqlState}",
                    (ex as PostgresException)?.SqlState ?? (ex.InnerException as PostgresException)?.SqlState);

                throw new ScheduleConflictException(
                    "The requested slot was taken by a concurrent booking. Retry with a different slot.",
                    ex);
            }
            catch (Exception ex)
            {
                await SafeRollbackAsync(transaction, cancellationToken);
                _logger.LogError(ex, "Booking transaction failed and was rolled back.");
                _context.ChangeTracker.Clear();
                throw;
            }
        });
    }

    private static bool IsContention(Exception exception)
    {
        // Contention can be nested arbitrarily deep: EF wraps the PostgresException in a
        // DbUpdateException, which the retry strategy then wraps in a transient-failure envelope.
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbUpdateConcurrencyException)
            {
                return true;
            }

            if (current is PostgresException postgres &&
                (postgres.SqlState == PostgresErrorCodes.SerializationFailure ||
                 postgres.SqlState == PostgresErrorCodes.DeadlockDetected))
            {
                return true;
            }
        }

        return false;
    }

    private async Task SafeRollbackAsync(IDbContextTransaction transaction, CancellationToken cancellationToken)
    {
        try
        {
            await transaction.RollbackAsync(cancellationToken);
        }
        catch (Exception rollbackFailure)
        {
            // A failed rollback must never mask the original booking failure.
            _logger.LogWarning(rollbackFailure, "Rollback failed; the connection may already be closed.");
        }
    }
}
