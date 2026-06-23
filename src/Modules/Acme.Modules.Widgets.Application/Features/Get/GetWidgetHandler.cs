using Acme.CQRS.Abstractions;
using Acme.DomainAbstractions;
using Acme.Modules.Widgets.Application.Contracts;
using Acme.Modules.Widgets.Application.Entities;

namespace Acme.Modules.Widgets.Application.Features.Get;

/// <summary>
/// Reads a single widget by composing a flat projection over the module's no-tracking read context
/// (ADR-0015 + #9/ADR-0016) — the query side never loads a tracked write aggregate.
/// </summary>
internal sealed class GetWidgetHandler(IWidgetsReadContext read)
    : IQueryHandler<GetWidgetQuery, WidgetDto>
{
    public async Task<Result<WidgetDto>> HandleAsync(
        GetWidgetQuery query,
        CancellationToken cancellationToken
    )
    {
        var dto = await read.FirstOrDefaultAsync(
            read.Query<WidgetEntity>()
                .Where(w => w.Id == query.Id)
                .Select(w => new WidgetDto
                {
                    Id = w.Id,
                    Name = w.Name,
                    Quantity = w.Quantity,
                    CreatedAt = w.CreatedAt,
                }),
            cancellationToken
        );

        return dto is null
            ? Result<WidgetDto>.Failure(WidgetErrors.NotFound(query.Id))
            : Result.Success(dto);
    }
}
