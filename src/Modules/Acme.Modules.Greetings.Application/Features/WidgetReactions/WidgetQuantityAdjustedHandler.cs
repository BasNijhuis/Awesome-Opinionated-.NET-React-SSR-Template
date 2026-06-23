using Acme.CQRS.Abstractions;
using Acme.Kernel.Domain.DomainEvents;
using Acme.Modules.Greetings.Domain;
using Acme.Modules.Greetings.Domain.Contracts;

namespace Acme.Modules.Greetings.Application.Features.WidgetReactions;

/// <summary>
/// Cross-module reaction: when a widget's quantity is adjusted, the Greetings module records an
/// announcement greeting. The handler runs in-process inside the Widgets command's transaction
/// (ADR-0016) and only <em>stages</em> the new aggregate via the repository — it must not commit
/// (no <c>IUnitOfWork</c>; architecture-test enforced). The coordinating unit of work saves both the
/// widget update and this greeting atomically.
/// </summary>
internal sealed class WidgetQuantityAdjustedHandler(IGreetingRepository repository)
    : IDomainEventHandler<WidgetQuantityAdjusted>
{
    public Task HandleAsync(WidgetQuantityAdjusted domainEvent, CancellationToken cancellationToken)
    {
        var greeting = Greeting.Create(
            new WidgetAdjustmentAnnouncement(
                $"Widget '{domainEvent.Name}' quantity changed from {domainEvent.OldQuantity} "
                    + $"to {domainEvent.NewQuantity}."
            )
        );
        repository.Add(greeting);
        return Task.CompletedTask;
    }

    // The event already carries everything the greeting needs, so the spec is built from it (a domain
    // method's inputs are a spec interface — ADR-0018).
    private sealed record WidgetAdjustmentAnnouncement(string Message) : ICreateGreetingSpec;
}
