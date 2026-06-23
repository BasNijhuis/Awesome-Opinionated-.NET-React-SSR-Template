using Acme.CQRS.Abstractions;
using Acme.Modules.Widgets.Application;
using Acme.Modules.Widgets.Domain;
using Acme.Modules.Widgets.Domain.Contracts;

namespace Acme.Modules.Widgets.Infrastructure.Persistence;

/// <summary>
/// No-database repository that mirrors <see cref="EfWidgetRepository"/>: reads rehydrate a fresh
/// aggregate from the stored entity, and Add maps the aggregate back onto an entity in the store right
/// away (the entity is the working set, like EF's tracked entity).
/// </summary>
public sealed class InMemoryWidgetRepository(InMemoryWidgetStore store, TrackedAggregates tracked)
    : IWidgetRepository
{
    public void Add(Widget widget)
    {
        var entity = WidgetPersistenceMapper.ToEntity(widget);
        if (!store.Widgets.TryAdd(widget.Id, entity))
        {
            throw new InvalidOperationException("Widget id collision.");
        }

        tracked.Track(widget);
    }

    public Task<Widget?> GetByIdAsync(WidgetId id, CancellationToken cancellationToken)
    {
        var entity = store.Widgets.GetValueOrDefault(id);
        return Task.FromResult(entity is null ? null : WidgetPersistenceMapper.ToDomain(entity));
    }
}
