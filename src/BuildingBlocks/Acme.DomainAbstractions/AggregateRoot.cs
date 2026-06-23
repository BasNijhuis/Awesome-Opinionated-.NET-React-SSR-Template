namespace Acme.DomainAbstractions;

/// <summary>
/// Base for an aggregate root that records <see cref="IDomainEvent"/>s as it transitions. The unit of
/// work drains them (via <c>TrackedAggregates.DequeueEvents</c>) and dispatches them in-process before
/// committing the transaction, so cross-aggregate reactions stay atomic with the change that caused them.
/// <para>
/// Aggregates are immutable <c>record</c>s (#23): both state and the domain-events outbox are immutable.
/// A transition returns a <em>new</em> instance built with <c>with</c>, then raises its own events with
/// <see cref="RaiseEvent{TSelf}"/>, which itself returns a new instance carrying the appended event. The
/// outbox is an <c>init</c>-only <see cref="IReadOnlyList{T}"/>; because it is immutable, a plain
/// <c>with</c> safely shares the same list reference and so carries the prior instance's still-pending
/// events forward. The aggregate cannot clear its own events in place (it is immutable) — instead
/// <see cref="DequeueEvents"/> returns the events plus a cleared copy, which <c>TrackedAggregates</c>
/// swaps in.
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
    /// accumulating stale instances.
    /// </summary>
    public abstract bool HasSameIdentity(AggregateRoot other);

    /// <summary>
    /// Records a domain event functionally: returns a <em>new</em> instance whose outbox is the prior
    /// pending events plus <paramref name="domainEvent"/>. Callers use the concrete aggregate type as
    /// <typeparamref name="TSelf"/>, e.g. <c>next.RaiseEvent&lt;Session&gt;(evt)</c>.
    /// </summary>
    protected TSelf RaiseEvent<TSelf>(IDomainEvent domainEvent)
        where TSelf : AggregateRoot =>
        (TSelf)(this with { DomainEvents = [.. DomainEvents, domainEvent] });

    /// <summary>
    /// Functional dequeue: returns the pending events together with a <em>cleared copy</em> of this
    /// aggregate (same identity, empty outbox). The aggregate is immutable, so it can't clear itself in
    /// place — the caller (the unit of work via <c>TrackedAggregates</c>) swaps in the cleared instance.
    /// </summary>
    public (IReadOnlyList<IDomainEvent> Events, AggregateRoot Cleared) DequeueEvents() =>
        (DomainEvents, this with { DomainEvents = [] });
}
