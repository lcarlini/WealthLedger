namespace WealthLedger.Application.Repositories;

/// <summary>
/// Groups several repository writes into one all-or-nothing operation.
/// Nested calls join the outer operation instead of starting another transaction.
/// </summary>
public interface IUnitOfWork
{
    Task ExecuteAsync(Func<Task> work, CancellationToken cancellationToken = default);
}
