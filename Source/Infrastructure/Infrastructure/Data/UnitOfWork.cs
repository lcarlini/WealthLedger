using WealthLedger.Application.Repositories;

namespace WealthLedger.Infrastructure.Data;

public sealed class UnitOfWork(WealthLedgerDbContext db) : IUnitOfWork
{
    public async Task ExecuteAsync(Func<Task> work, CancellationToken cancellationToken = default)
    {
        if (db.Database.CurrentTransaction is not null)
        {
            await work();
            return;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await work();
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            // Discard in-memory entity state so nothing that was rolled back looks saved.
            db.ChangeTracker.Clear();
            throw;
        }
    }
}
