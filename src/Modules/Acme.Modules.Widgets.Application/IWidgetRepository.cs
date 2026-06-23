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

    Task<Widget?> GetByIdAsync(WidgetId id, CancellationToken cancellationToken);
}
