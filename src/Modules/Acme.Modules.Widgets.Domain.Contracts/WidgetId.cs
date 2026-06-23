using System.Text.Json.Serialization;

namespace Acme.Modules.Widgets.Domain.Contracts;

/// <summary>
/// Strongly-typed identity of a widget aggregate. Backed by a <see cref="Guid"/> generated with
/// <see cref="Guid.CreateVersion7()"/> (time-ordered — index-friendly). Crosses module/aggregate
/// boundaries by value; serializes as a bare GUID string (see <see cref="WidgetIdJsonConverter"/>)
/// so the public wire contract is unchanged.
/// </summary>
[JsonConverter(typeof(WidgetIdJsonConverter))]
public readonly record struct WidgetId(Guid Value)
{
    public static WidgetId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
