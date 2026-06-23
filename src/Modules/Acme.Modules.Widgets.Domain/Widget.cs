using Acme.DomainAbstractions;
using Acme.Modules.Widgets.Domain.Contracts;

namespace Acme.Modules.Widgets.Domain;

/// <summary>
/// The Widgets capability aggregate: a named item with a quantity, captured at a point in time.
/// <para>Immutable (#23): every transition returns a new <see cref="Widget"/>.</para>
/// </summary>
public sealed record Widget : AggregateRoot
{
    public WidgetId Id { get; private init; }
    public string Name { get; private init; }
    public int Quantity { get; private init; }
    public DateTimeOffset CreatedAt { get; private init; }

    public override bool HasSameIdentity(AggregateRoot other) => other is Widget o && o.Id == Id;

    private Widget(WidgetId id, string name, int quantity, DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        Quantity = quantity;
        CreatedAt = createdAt;
    }

    public static Widget Create(ICreateWidgetSpec spec)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(spec.Name);
        return new Widget(WidgetId.New(), spec.Name.Trim(), spec.Quantity, DateTimeOffset.UtcNow);
    }

    public static Widget Rehydrate(IWidgetState state) =>
        new(state.Id, state.Name, state.Quantity, state.CreatedAt);
}
