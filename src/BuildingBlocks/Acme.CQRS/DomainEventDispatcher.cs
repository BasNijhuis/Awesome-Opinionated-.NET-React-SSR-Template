using Acme.CQRS.Abstractions;
using Acme.DomainAbstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Acme.CQRS;

/// <summary>
/// Resolves and invokes every <see cref="IDomainEventHandler{TEvent}"/> registered for each event's
/// concrete type, in order. Runs within the caller's scope/transaction; a handler that throws
/// propagates so the transaction rolls back.
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
            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
            var handlers = serviceProvider.GetServices(handlerType);

            foreach (var handler in handlers)
            {
                await InvokeAsync(handler!, domainEvent, cancellationToken);
            }
        }
    }

    private static Task InvokeAsync(
        object handler,
        IDomainEvent domainEvent,
        CancellationToken cancellationToken
    )
    {
        var method = handler
            .GetType()
            .GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync))!;
        return (Task)method.Invoke(handler, [domainEvent, cancellationToken])!;
    }
}
