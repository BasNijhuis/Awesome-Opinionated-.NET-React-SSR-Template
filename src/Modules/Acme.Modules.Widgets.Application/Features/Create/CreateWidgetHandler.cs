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
        var widget = Widget.Create(command);
        repository.Add(widget);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await publisher.PublishAsync(
            new NotificationMessage("widgets", $"New widget: {widget.Name}"),
            cancellationToken
        );

        return Result.Success(
            new CreateWidgetResult
            {
                Id = widget.Id,
                Widget = new WidgetDto
                {
                    Id = widget.Id,
                    Name = widget.Name,
                    Quantity = widget.Quantity,
                    CreatedAt = widget.CreatedAt,
                },
            }
        );
    }
}
