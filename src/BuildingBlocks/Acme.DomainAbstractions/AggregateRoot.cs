namespace Acme.DomainAbstractions;

/// <summary>
/// The untyped handle on an aggregate root: what infrastructure holds when it doesn't know (and
/// shouldn't care) which aggregate it has — <c>TrackedAggregates</c> keeps a list of these, drains
/// their <see cref="IDomainEvent"/>s (via <c>TrackedAggregates.DequeueEvents</c>) and dispatches them
/// in-process before committing the transaction, so cross-aggregate reactions stay atomic with the
/// change that caused them.
/// <para>
/// Aggregates do not derive from this directly — they derive from <see cref="AggregateRoot{TSelf,TId}"/>,
/// which supplies <see cref="HasSameIdentity"/> and a self-typed <c>RaiseEvent</c>.
/// </para>
/// <para>
/// Aggregates are immutable <c>record</c>s (#23): both state and the domain-events outbox are immutable.
/// A transition returns a <em>new</em> instance built with <c>with</c>, then raises its own events with
/// <see cref="AggregateRoot{TSelf,TId}.RaiseEvent"/>, which itself returns a new instance carrying the
/// appended event. The outbox is an <c>init</c>-only <see cref="IReadOnlyList{T}"/>; because it is
/// immutable, a plain <c>with</c> safely shares the same list reference and so carries the prior
/// instance's still-pending events forward. The aggregate cannot clear its own events in place (it is
/// immutable) — instead <see cref="DequeueEvents"/> returns the events plus a cleared copy, which
/// <c>TrackedAggregates</c> swaps in.
/// </para>
/// </summary>
public abstract record AggregateRoot
{
    /// <summary>Events raised since the last drain, in order. Immutable — shared safely across <c>with</c>.</summary>
    public IReadOnlyList<IDomainEvent> DomainEvents { get; protected init; } = [];

    /// <summary>
    /// Identity equality for change tracking: true when <paramref name="other"/> is the same aggregate
    /// (same type + id). Records use <em>value</em> equality, which is wrong for tracking — a transition
    /// produces a new, value-distinct instance of the same aggregate. The unit of work tracks by this so
    /// a new instance replaces the prior one (which carried its pending events forward) rather than
    /// accumulating stale instances. Implemented once by <see cref="AggregateRoot{TSelf,TId}"/>.
    /// </summary>
    public abstract bool HasSameIdentity(AggregateRoot other);

    /// <summary>
    /// Functional dequeue: returns the pending events together with a <em>cleared copy</em> of this
    /// aggregate (same identity, empty outbox). The aggregate is immutable, so it can't clear itself in
    /// place — the caller (the unit of work via <c>TrackedAggregates</c>) swaps in the cleared instance.
    /// </summary>
    public (IReadOnlyList<IDomainEvent> Events, AggregateRoot Cleared) DequeueEvents() =>
        (DomainEvents, this with { DomainEvents = [] });
}

/// <summary>
/// Base for a concrete aggregate root, typed on itself and on its identity: a <em>self type</em>
/// (<typeparamref name="TSelf"/>, the curiously recurring pattern) plus the id type
/// (<typeparamref name="TId"/>). Declaring both here is what lets the base supply the two things every
/// aggregate would otherwise hand-write — <see cref="HasSameIdentity"/> and a <c>RaiseEvent</c> that
/// returns the concrete aggregate type:
/// <code>
/// public sealed record Widget : AggregateRoot&lt;Widget, WidgetId&gt;
/// {
///     public override WidgetId Id { get; }
///     // …
///     public Widget Rename(string name) => (this with { Name = name }).RaiseEvent(new WidgetRenamed(Id));
/// }
/// </code>
/// <para>
/// <typeparamref name="TSelf"/> <strong>must</strong> be the declaring record — <c>RaiseEvent</c> casts
/// the <c>with</c>-clone to it, and the constraint alone can't express that (<c>record Gadget :
/// AggregateRoot&lt;Widget, WidgetId&gt;</c> compiles). The <c>ACME009</c> analyzer rule enforces it at
/// the declaration.
/// </para>
/// </summary>
public abstract record AggregateRoot<TSelf, TId> : AggregateRoot
    where TSelf : AggregateRoot<TSelf, TId>
    where TId : IEquatable<TId>
{
    /// <summary>
    /// The aggregate's identity. Declared here so the base can compare identities; a derived aggregate
    /// overrides it with a get-only auto-property assigned in its constructor — the record copy
    /// constructor copies backing fields, so <c>with</c> carries the id forward untouched.
    /// </summary>
    public abstract TId Id { get; }

    /// <summary>
    /// Same aggregate = same self type + same id. Sealed: identity is structural to the pattern, not a
    /// per-aggregate decision.
    /// </summary>
    public sealed override bool HasSameIdentity(AggregateRoot other) =>
        other is TSelf o && EqualityComparer<TId>.Default.Equals(Id, o.Id);

    /// <summary>
    /// Records a domain event functionally: returns a <em>new</em> instance (typed as the concrete
    /// aggregate) whose outbox is the prior pending events plus <paramref name="domainEvent"/>.
    /// </summary>
    protected TSelf RaiseEvent(IDomainEvent domainEvent) =>
        (TSelf)(this with { DomainEvents = [.. DomainEvents, domainEvent] });
}
