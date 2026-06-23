using Acme.Modules.Widgets.Domain.Contracts;

namespace Acme.Modules.Widgets.Domain;

/// <summary>
/// Read-only hydration spec for the <see cref="Widget"/> aggregate. Implemented by the module's
/// persistence entity so the aggregate rehydrates straight from it.
/// </summary>
public interface IWidgetState
{
    WidgetId Id { get; }
    string Name { get; }
    int Quantity { get; }
    DateTimeOffset CreatedAt { get; }
}
