using Acme.CQRS.Abstractions;
using Acme.DomainAbstractions;
using Acme.Modules.Widgets.Application.Contracts;
using Acme.Modules.Widgets.Application.Entities;

namespace Acme.Modules.Widgets.Application.Features.List;

/// <summary>
/// Lists all widgets by composing a flat projection over the module's no-tracking read context
/// (ADR-0015 + #9/ADR-0016), newest first.
/// </summary>
internal sealed class ListWidgetsHandler(IWidgetsReadContext read)
    : IQueryHandler<ListWidgetsQuery, IReadOnlyList<WidgetDto>>
{
    public async Task<Result<IReadOnlyList<WidgetDto>>> HandleAsync(
        ListWidgetsQuery query,
        CancellationToken cancellationToken
    )
    {
        var rows = await read.ToListAsync(
            read.Query<WidgetEntity>()
                .OrderByDescending(w => w.CreatedAt)
                .Select(w => new WidgetDto
                {
                    Id = w.Id,
                    Name = w.Name,
                    Quantity = w.Quantity,
                    CreatedAt = w.CreatedAt,
                }),
            cancellationToken
        );

        return rows;
    }
}
