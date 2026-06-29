using Acme.CQRS.Abstractions;
using Acme.DomainAbstractions;
using Acme.Kernel.Application.Common.Interfaces;
using Acme.Kernel.Contracts;
using Acme.Modules.Widgets.Application.Contracts;
using Acme.Modules.Widgets.Domain;

namespace Acme.Modules.Widgets.Application.Features.Create;

internal sealed class CreateWidgetHandler(
    IWidgetRepository repository,
    IUnitOfWork unitOfWork,
    INotificationPublisher publisher
) : ICommandHandler<CreateWidgetCommand, CreateWidgetResult>
{
    public async Task<Result<CreateWidgetResult>> HandleAsync(
        CreateWidgetCommand command,
        CancellationToken cancellationToken
    )
    {
        var created = Widget.Create(command);
        if (created.IsFailure)
        {
            return created.Errors; // propagate the domain rule failure
        }

        var widget = created.Value;
        repository.Add(widget);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await publisher.PublishAsync(
            new NotificationMessage("widgets", $"New widget: {widget.Name}"),
            cancellationToken
        );

        return new CreateWidgetResult
        {
            Id = widget.Id,
            Widget = new WidgetDto
            {
                Id = widget.Id,
                Name = widget.Name,
                Quantity = widget.Quantity,
                CreatedAt = widget.CreatedAt,
            },
        };
    }
}
