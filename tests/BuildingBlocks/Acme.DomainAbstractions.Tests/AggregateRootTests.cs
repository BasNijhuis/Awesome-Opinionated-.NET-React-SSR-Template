using Acme.DomainAbstractions;

namespace Acme.DomainAbstractions.Tests;

public sealed class AggregateRootTests
{
    private sealed record ThingHappened(int Value) : IDomainEvent;

    private sealed record TestAggregate : AggregateRoot<TestAggregate, Guid>
    {
        // Get-only and assigned once: the record copy constructor copies the backing field, so a
        // 'with' clone (and a cleared copy) carries the same identity forward.
        public override Guid Id { get; } = Guid.CreateVersion7();

        // A transition is functional: it returns a new instance carrying the appended event,
        // with prior pending events carried forward via the immutable outbox.
        public TestAggregate Do(int value) => RaiseEvent(new ThingHappened(value));
    }

    [Fact]
    public void Raised_events_are_exposed_in_order()
    {
        // Arrange — one event already raised
        var aggregate = new TestAggregate().Do(1);

        // Act
        aggregate = aggregate.Do(2);

        // Assert — the second raise carried the first event forward (immutable outbox shared via 'with')
        aggregate.DomainEvents.Should().Equal(new ThingHappened(1), new ThingHappened(2));
    }

    [Fact]
    public void Raising_does_not_mutate_the_prior_instance()
    {
        // Arrange — the aggregate is immutable: raising returns a new instance and leaves the prior one
        // untouched. Clearing the outbox is done by the drain via a cleared copy, not on the aggregate.
        var first = new TestAggregate().Do(1);

        // Act
        var second = first.Do(2);

        // Assert
        first.DomainEvents.Should().Equal(new ThingHappened(1));
        second.DomainEvents.Should().Equal(new ThingHappened(1), new ThingHappened(2));
    }

    [Fact]
    public void A_cleared_copy_drops_pending_events()
    {
        // Arrange — this models how TrackedAggregates clears an immutable aggregate's outbox.
        var aggregate = new TestAggregate().Do(1).Do(2);

        // Act
        var (_, cleared) = aggregate.DequeueEvents();

        // Assert
        cleared.DomainEvents.Should().BeEmpty();
        aggregate.DomainEvents.Should().HaveCount(2);
    }

    [Fact]
    public void A_transitioned_instance_has_the_same_identity()
    {
        // Arrange — records compare by value, so a transition yields a value-distinct instance; the
        // unit of work must still recognise it as the same aggregate.
        var aggregate = new TestAggregate();

        // Act
        var next = aggregate.Do(1);

        // Assert
        next.Should().NotBe(aggregate);
        aggregate.HasSameIdentity(next).Should().BeTrue();
    }

    [Fact]
    public void A_different_aggregate_has_a_different_identity()
    {
        // Arrange / Act — two aggregates, each with its own id
        var aggregate = new TestAggregate();
        var other = new TestAggregate();

        // Assert
        aggregate.HasSameIdentity(other).Should().BeFalse();
    }

    [Fact]
    public void A_fresh_aggregate_has_no_events()
    {
        // Arrange / Act
        var aggregate = new TestAggregate();

        // Assert
        aggregate.DomainEvents.Should().BeEmpty();
    }
}
