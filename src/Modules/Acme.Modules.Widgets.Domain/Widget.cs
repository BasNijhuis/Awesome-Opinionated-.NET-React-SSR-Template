using Acme.DomainAbstractions;
using Acme.Kernel.Domain.DomainEvents;
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

    /// <summary>
    /// Creates a widget from <paramref name="spec"/>. A blank name is an expected, recoverable failure
    /// (a <see cref="ErrorCategory.Validation"/>) returned as a <see cref="Result{T}"/> rather than
    /// thrown; the error code is the field name so it slots into the API's validation field map.
    /// </summary>
    public static Result<Widget> Create(ICreateWidgetSpec spec)
    {
        if (string.IsNullOrWhiteSpace(spec.Name))
        {
            return Error.Validation(nameof(spec.Name), "Name is required.");
        }

        return new Widget(WidgetId.New(), spec.Name.Trim(), spec.Quantity, DateTimeOffset.UtcNow);
    }

    public static Widget Rehydrate(IWidgetState state) =>
        new(state.Id, state.Name, state.Quantity, state.CreatedAt);

    /// <summary>
    /// Adjusts the quantity by <paramref name="delta"/> (positive to add stock, negative to remove).
    /// Immutable transition (#23): returns a <em>new</em> <see cref="Widget"/> that raises a
    /// <see cref="WidgetQuantityAdjusted"/> domain event. The quantity may never go negative — that's
    /// an expected, recoverable failure (a <see cref="ErrorCategory.Conflict"/>), not an exception.
    /// </summary>
    public Result<Widget> AdjustQuantity(int delta)
    {
        var newQuantity = Quantity + delta;
        if (newQuantity < 0)
        {
            return Error.Conflict(
                "widget_quantity_negative",
                $"Adjusting '{Name}' by {delta} would make its quantity negative ({newQuantity})."
            );
        }

        return (this with { Quantity = newQuantity }).RaiseEvent<Widget>(
            new WidgetQuantityAdjusted(Id.Value, Name, Quantity, newQuantity)
        );
    }
}
