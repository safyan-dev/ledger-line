using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Persistence.Abstractions;

public interface IAppDbContext
{
    DbSet<TEntity> Set<TEntity>()
        where TEntity : class;

    ChangeTracker ChangeTracker { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);

    Task RollbackAsync(CancellationToken cancellationToken = default);
}

public interface IUnitOfWork
{
    bool HasActiveTransaction { get; }

    bool HasPendingDomainEvents { get; }

    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    Task<Result> DispatchDomainEventsAsync(CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
