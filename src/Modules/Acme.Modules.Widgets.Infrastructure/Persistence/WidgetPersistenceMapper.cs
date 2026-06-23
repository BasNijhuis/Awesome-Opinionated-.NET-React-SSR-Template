using Acme.Modules.Widgets.Application.Entities;
using Acme.Modules.Widgets.Domain;

namespace Acme.Modules.Widgets.Infrastructure.Persistence;

/// <summary>
/// Maps between the <see cref="Widget"/> aggregate and its persistence entity. Hydration reads
/// straight from the entity (it implements <c>IWidgetState</c>); persistence reads the aggregate's
/// public surface.
/// </summary>
internal static class WidgetPersistenceMapper
{
    public static Widget ToDomain(WidgetEntity entity) => Widget.Rehydrate(entity);

    public static WidgetEntity ToEntity(Widget widget) =>
        new()
        {
            Id = widget.Id,
            Name = widget.Name,
            Quantity = widget.Quantity,
            CreatedAt = widget.CreatedAt,
        };

    public static void ApplyToEntity(Widget widget, WidgetEntity entity)
    {
        entity.Name = widget.Name;
        entity.Quantity = widget.Quantity;
        entity.CreatedAt = widget.CreatedAt;
    }
}
