namespace Acme.CQRS.Abstractions;

/// <summary>
/// Drains and dispatches the tracked aggregates' domain events, looping so events raised by handlers
/// (cascades) are also dispatched, until none remain. Called by the unit of work before it commits,
/// so reactions run in the same transaction as the change that caused them (D1).
/// </summary>
public static class DomainEventDrain
{
    public static async Task DispatchPendingAsync(
        TrackedAggregates tracked,
        IDomainEventDispatcher dispatcher,
        CancellationToken cancellationToken
    )
    {
        var events = tracked.DequeueEvents();
        while (events.Count > 0)
        {
            await dispatcher.DispatchAsync(events, cancellationToken);
            events = tracked.DequeueEvents();
        }
    }
}
