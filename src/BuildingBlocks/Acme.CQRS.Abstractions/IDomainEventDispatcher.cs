using Acme.DomainAbstractions;

namespace Acme.CQRS.Abstractions;

/// <summary>
/// Dispatches domain events to their <see cref="IDomainEventHandler{TEvent}"/>s. Called by the unit
/// of work after aggregates are mutated and before the transaction commits.
/// </summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken);
}
