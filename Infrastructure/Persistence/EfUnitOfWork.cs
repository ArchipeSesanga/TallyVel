using Microsoft.EntityFrameworkCore.Storage;
using Infrastructure.Data;
using Domain;

namespace Infrastructure.Persistence;

public sealed class EfUnitOfWork(TallyVelDbContext db) : IUnitOfWork
{
    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct) =>
        new EfTransaction(await db.Database.BeginTransactionAsync(ct));

    private sealed class EfTransaction(IDbContextTransaction tx) : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken ct) => tx.CommitAsync(ct);
        public ValueTask DisposeAsync() => tx.DisposeAsync();
    }
}
