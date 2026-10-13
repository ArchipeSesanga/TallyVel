namespace Domain;

/// <summary>
/// Lets a service make several repository calls succeed or fail together.
/// </summary>
public interface IUnitOfWork
{
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct);
}

/// <summary>Rolls back on dispose unless <see cref="CommitAsync"/> was called.</summary>
public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct);
}
