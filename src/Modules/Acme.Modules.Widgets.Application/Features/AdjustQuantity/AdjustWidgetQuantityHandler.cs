using Acme.CQRS.Abstractions;
using Acme.DomainAbstractions;
using Acme.Kernel.Application.Common.Interfaces;
using Acme.Kernel.Contracts;
using Acme.Modules.Widgets.Application.Contracts;

namespace Acme.Modules.Widgets.Application.Features.AdjustQuantity;

/// <summary>
/// Applies an immutable <c>Widget.AdjustQuantity</c> transition, stages it, then commits. The
/// transition raises a <c>WidgetQuantityAdjusted</c> domain event that the Greetings module reacts to
/// inside the same transaction (ADR-0016).
/// <para>
/// The response is mapped straight from the committed aggregate (like the Create handlers) — no
/// read-back round trip: the cross-module reaction records a <em>greeting</em>, it never touches this
/// widget, so the aggregate is already the authoritative post-commit view. (Re-read after commit only
/// when a reaction can change the very aggregate you're returning — see backend-development.md.)
/// </para>
/// </summary>
internal sealed class AdjustWidgetQuantityHandler(
    IWidgetRepository repository,
    IUnitOfWork unitOfWork,
    INotificationPublisher publisher
) : ICommandHandler<AdjustWidgetQuantityCommand, WidgetDto>
{
    public async Task<Result<WidgetDto>> HandleAsync(
        AdjustWidgetQuantityCommand command,
        CancellationToken cancellationToken
    )
    {
        var widget = await repository.GetByIdAsync(command.Id, cancellationToken);
        if (widget is null)
        {
            return Result<WidgetDto>.Failure(WidgetErrors.NotFound(command.Id));
        }

        var adjusted = widget.AdjustQuantity(command.Delta);
        if (adjusted.IsFailure)
        {
            return Result<WidgetDto>.Failure(adjusted.Errors); // propagate the domain rule failure
        }

        var updated = adjusted.Value;
        repository.Update(updated); // sync — stages onto the tracked entity
        await unitOfWork.SaveChangesAsync(cancellationToken); // dispatches events + commits atomically

        await publisher.PublishAsync(
            new NotificationMessage(
                "widgets",
                $"Widget '{updated.Name}' is now at quantity {updated.Quantity}."
            ),
            cancellationToken
        );

        return Result.Success(
            new WidgetDto
            {
                Id = updated.Id,
                Name = updated.Name,
                Quantity = updated.Quantity,
                CreatedAt = updated.CreatedAt,
            }
        );
    }
}
