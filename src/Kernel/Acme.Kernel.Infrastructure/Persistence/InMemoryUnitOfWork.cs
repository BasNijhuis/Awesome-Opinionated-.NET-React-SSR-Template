using Acme.CQRS.Abstractions;
using Acme.Kernel.Application.Common.Interfaces;

namespace Acme.Kernel.Infrastructure.Persistence;

public sealed class InMemoryUnitOfWork(TrackedAggregates tracked, IDomainEventDispatcher dispatcher)
    : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        // The repositories write through to the store eagerly (like EF's tracked entities), so saving
        // only has to drain domain events and their cascades — dispatched here, before the logical
        // commit, exactly as the EF unit of work does (D1).
        DomainEventDrain.DispatchPendingAsync(tracked, dispatcher, cancellationToken);
}
