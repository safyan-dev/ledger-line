using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PaymentReconciliation.Persistence.Abstractions;
using PaymentReconciliation.SharedKernel.Domain;
using PaymentReconciliation.SharedKernel.Events;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Persistence;

public sealed class UnitOfWork(AppDbContext dbContext, IDomainEventDispatcher domainEventDispatcher) : IUnitOfWork
{
    private IDbContextTransaction? _transaction;

    public bool HasActiveTransaction => _transaction is not null;

    public bool HasPendingDomainEvents
        => dbContext.ChangeTracker
            .Entries<AggregateRoot>()
            .Any(entry => entry.Entity.DomainEvents.Count > 0);

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        return new UnitOfWorkTransaction(_transaction);
    }

    public async Task<Result> DispatchDomainEventsAsync(CancellationToken cancellationToken = default)
    {
        var domainEvents = dbContext.ChangeTracker
            .Entries<AggregateRoot>()
            .SelectMany(entry => entry.Entity.DequeueDomainEvents())
            .ToList();

        foreach (var domainEvent in domainEvents)
        {
            var result = await domainEventDispatcher.PublishAsync(domainEvent, cancellationToken);
            if (result.IsFailure)
            {
                return result;
            }
        }

        return Result.Success();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);

    private sealed class UnitOfWorkTransaction(IDbContextTransaction transaction) : IUnitOfWorkTransaction
    {
        private bool _completed;

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            await transaction.CommitAsync(cancellationToken);
            _completed = true;
        }

        public async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            if (!_completed)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            _completed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (!_completed)
            {
                await transaction.RollbackAsync();
            }

            await transaction.DisposeAsync();
        }
    }
}
