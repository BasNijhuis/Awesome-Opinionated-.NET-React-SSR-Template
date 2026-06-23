using Acme.CQRS.Abstractions;
using Acme.DomainAbstractions;
using Acme.Kernel.Infrastructure.Persistence;

namespace Acme.Kernel.Infrastructure.Tests;

public sealed class UnitOfWorkDomainEventTests
{
    private sealed record ThingHappened : IDomainEvent;

    private sealed record TestAggregate : AggregateRoot
    {
        public TestAggregate Do() => RaiseEvent<TestAggregate>(new ThingHappened());

        public override bool HasSameIdentity(AggregateRoot other) => ReferenceEquals(this, other);
    }

    private sealed class SpyDispatcher : IDomainEventDispatcher
    {
        public List<IDomainEvent> Dispatched { get; } = [];

        public Task DispatchAsync(
            IEnumerable<IDomainEvent> domainEvents,
            CancellationToken cancellationToken
        )
        {
            Dispatched.AddRange(domainEvents);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task SaveChanges_dispatches_tracked_aggregates_domain_events_and_clears_them()
    {
        // Arrange — a tracked aggregate with one raised event (aggregates are immutable, so a
        // transition returns a new instance; we track that)
        var tracked = new TrackedAggregates();
        var dispatcher = new SpyDispatcher();
        var unitOfWork = new InMemoryUnitOfWork(tracked, dispatcher);
        tracked.Track(new TestAggregate().Do());

        // Act
        await unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        dispatcher.Dispatched.Should().ContainSingle().Which.Should().BeOfType<ThingHappened>();
        tracked
            .DequeueEvents()
            .Should()
            .BeEmpty("the drain replaced the tracked aggregate with a cleared copy");
    }

    [Fact]
    public async Task SaveChanges_with_no_raised_events_dispatches_nothing()
    {
        // Arrange — a tracked aggregate that raised nothing
        var tracked = new TrackedAggregates();
        var dispatcher = new SpyDispatcher();
        var unitOfWork = new InMemoryUnitOfWork(tracked, dispatcher);
        tracked.Track(new TestAggregate());

        // Act
        await unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        dispatcher.Dispatched.Should().BeEmpty();
    }
}
