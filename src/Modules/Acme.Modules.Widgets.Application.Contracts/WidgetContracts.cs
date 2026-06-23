using Acme.CQRS.Abstractions;
using Acme.Modules.Widgets.Domain.Contracts;

namespace Acme.Modules.Widgets.Application.Contracts;

public sealed record CreateWidgetCommand : ICommand<CreateWidgetResult>, ICreateWidgetSpec
{
    public required string Name { get; init; }
    public required int Quantity { get; init; }
}

public sealed record CreateWidgetResult
{
    public required WidgetId Id { get; init; }
    public required WidgetDto Widget { get; init; }
}

public sealed record GetWidgetQuery : IQuery<WidgetDto>
{
    public required WidgetId Id { get; init; }
}

public sealed record ListWidgetsQuery : IQuery<IReadOnlyList<WidgetDto>>;

public sealed record WidgetDto
{
    public required WidgetId Id { get; init; }
    public required string Name { get; init; }
    public required int Quantity { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
