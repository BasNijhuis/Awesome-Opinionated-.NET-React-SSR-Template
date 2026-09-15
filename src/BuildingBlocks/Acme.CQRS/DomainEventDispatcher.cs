using Acme.CQRS.Abstractions;
using Acme.DomainAbstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Acme.CQRS;

/// <summary>
/// Invokes every <see cref="IDomainEventHandler{TEvent}"/> registered for each event's concrete type,
/// in order, through the typed adapter registered under that type. Runs within the caller's
/// scope/transaction; a handler that throws propagates so the transaction rolls back.
/// </summary>
public sealed class DomainEventDispatcher(IServiceProvider serviceProvider) : IDomainEventDispatcher
{
    public async Task DispatchAsync(
        IEnumerable<IDomainEvent> domainEvents,
        CancellationToken cancellationToken
    )
    {
        foreach (var domainEvent in domainEvents)
        {
            // No adapter means no handler is registered for this event — a normal, silent no-op.
            var adapter = serviceProvider.GetKeyedService<IDomainEventHandlerAdapter>(
                domainEvent.GetType()
            );

            if (adapter is not null)
            {
                await adapter.HandleAllAsync(domainEvent, cancellationToken);
            }
        }
    }
}
