using Acme.Modules.Widgets.Domain;
using Acme.Modules.Widgets.Domain.Contracts;

namespace Acme.Modules.Widgets.Application.Entities;

// Persistence entity for the Widgets module (own schema). The strongly-typed id (an EF value
// converter maps it to a uuid column) and it implements the domain hydration spec (IWidgetState)
// so Widget.Rehydrate reconstructs the aggregate straight from it. EF mapping lives in
// Widgets.Infrastructure (WidgetsDbContext).
public sealed class WidgetEntity : IWidgetState
{
    public WidgetId Id { get; set; }
    public required string Name { get; set; }
    public int Quantity { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
