using Acme.DomainAbstractions;

namespace Acme.CQRS.Abstractions;

/// <summary>
/// Scoped collector of the aggregate roots a request mutated. Repositories register aggregates as
/// they stage them; the unit of work drains their domain events with <see cref="DequeueEvents"/> and
/// dispatches them before committing. Aggregates are immutable (#23) and cannot clear their own events,
/// so draining replaces each tracked entry with a cleared copy — that way a dispatch loop can pick up
/// events raised by handlers (cascades) until none remain. Lives here so every persistence module
/// (across contexts) can register into the same scoped instance.
/// </summary>
public sealed class TrackedAggregates
{
    private readonly List<AggregateRoot> _aggregates = [];

    /// <summary>
    /// Registers the latest instance of an aggregate. Aggregates are immutable records (#23), so a
    /// transition stages a <em>new</em> instance of the same aggregate; we track by identity
    /// (<see cref="AggregateRoot.HasSameIdentity"/>, not record value equality) and <strong>replace</strong>
    /// any prior instance with the same id — the new one carries the prior's still-pending events
    /// forward (the immutable outbox is shared across <c>with</c>), so we drain the latest and don't
    /// accumulate stale instances.
    /// </summary>
    public void Track(AggregateRoot aggregate)
    {
        var existingIndex = _aggregates.FindIndex(tracked => tracked.HasSameIdentity(aggregate));
        if (existingIndex >= 0)
        {
            _aggregates[existingIndex] = aggregate;
        }
        else
        {
            _aggregates.Add(aggregate);
        }
    }

    /// <summary>
    /// Returns every tracked aggregate's pending events and clears them. Because aggregates are
    /// immutable, each one's <see cref="AggregateRoot.DequeueEvents"/> hands back its events plus a
    /// cleared copy, which we swap in — so each event dispatches once and a re-drain after handler
    /// cascades sees only the freshly raised events.
    /// </summary>
    public IReadOnlyList<IDomainEvent> DequeueEvents()
    {
        var events = new List<IDomainEvent>();
        for (var i = 0; i < _aggregates.Count; i++)
        {
            var (raised, cleared) = _aggregates[i].DequeueEvents();
            events.AddRange(raised);
            _aggregates[i] = cleared;
        }

        return events;
    }
}
