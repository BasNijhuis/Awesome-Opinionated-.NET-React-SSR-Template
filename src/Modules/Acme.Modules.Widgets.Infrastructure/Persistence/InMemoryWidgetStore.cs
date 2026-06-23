using System.Collections.Concurrent;
using Acme.Modules.Widgets.Application.Entities;
using Acme.Modules.Widgets.Domain.Contracts;

namespace Acme.Modules.Widgets.Infrastructure.Persistence;

/// <summary>
/// Backing store for the no-database path. It holds the persistence <see cref="WidgetEntity"/> (the
/// row-equivalent), not live aggregates: the repository rehydrates a fresh aggregate from the entity
/// on every read and applies writes back onto it, so the in-memory path mirrors EF's
/// identity-per-load behavior.
/// </summary>
public sealed class InMemoryWidgetStore
{
    internal ConcurrentDictionary<WidgetId, WidgetEntity> Widgets { get; } = new();
}
