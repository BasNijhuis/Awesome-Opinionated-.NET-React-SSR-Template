using Acme.Modules.Widgets.Domain;
using Acme.Modules.Widgets.Domain.Contracts;

namespace Acme.Modules.Widgets.Application;

/// <summary>
/// Loads and persists the <see cref="Widget"/> aggregate. Implemented over the Widgets module's own
/// context; the application layer stays off EF Core.
/// </summary>
public interface IWidgetRepository
{
    void Add(Widget widget);

    /// <summary>
    /// Stages an update to an aggregate previously loaded via <see cref="GetByIdAsync"/> in this scope
    /// (it must be tracked). Synchronous — like <see cref="Add"/>, it queues the change; the unit of
    /// work commits it. See ADR-0009 §3.
    /// </summary>
    void Update(Widget widget);

    Task<Widget?> GetByIdAsync(WidgetId id, CancellationToken cancellationToken);
}
