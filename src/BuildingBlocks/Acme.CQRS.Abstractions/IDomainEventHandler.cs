using Acme.DomainAbstractions;

namespace Acme.CQRS.Abstractions;

/// <summary>
/// Handles a domain event in-process, inside the same transaction as the change that raised it.
/// A handler throwing rolls the transaction back — domain-event reactions are atomic, not best-effort.
/// </summary>
public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}
