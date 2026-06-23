using Acme.CQRS.Abstractions;
using Acme.Modules.Widgets.Application;
using Acme.Modules.Widgets.Domain;
using Acme.Modules.Widgets.Domain.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Acme.Modules.Widgets.Infrastructure.Persistence;

public sealed class EfWidgetRepository(WidgetsDbContext db, TrackedAggregates tracked)
    : IWidgetRepository
{
    public void Add(Widget widget)
    {
        db.Widgets.Add(WidgetPersistenceMapper.ToEntity(widget));
        tracked.Track(widget);
    }

    public async Task<Widget?> GetByIdAsync(WidgetId id, CancellationToken cancellationToken)
    {
        var entity =
            db.Widgets.Local.FirstOrDefault(w => w.Id == id)
            ?? await db.Widgets.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

        return entity is null ? null : WidgetPersistenceMapper.ToDomain(entity);
    }
}
