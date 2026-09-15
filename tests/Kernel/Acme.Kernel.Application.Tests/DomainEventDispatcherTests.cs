using Acme.CQRS;
using Acme.CQRS.Abstractions;
using Acme.DomainAbstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Acme.Kernel.Application.Tests;

public sealed class DomainEventDispatcherTests
{
    private sealed record ThingHappened : IDomainEvent;

    private sealed record OtherThingHappened : IDomainEvent;

    private sealed record UnhandledThingHappened : IDomainEvent;

    /// <summary>Shared recorder so handler invocations can be asserted in order.</summary>
    private sealed class Journal
    {
        public List<string> Entries { get; } = [];
    }

    private sealed class FirstThingHandler(Journal journal) : IDomainEventHandler<ThingHappened>
    {
        public Task HandleAsync(ThingHappened domainEvent, CancellationToken cancellationToken)
        {
            journal.Entries.Add("first");
            return Task.CompletedTask;
        }
    }

    private sealed class SecondThingHandler(Journal journal) : IDomainEventHandler<ThingHappened>
    {
        public Task HandleAsync(ThingHappened domainEvent, CancellationToken cancellationToken)
        {
            journal.Entries.Add("second");
            return Task.CompletedTask;
        }
    }

    private sealed class OtherThingHandler(Journal journal)
        : IDomainEventHandler<OtherThingHappened>
    {
        public Task HandleAsync(OtherThingHappened domainEvent, CancellationToken cancellationToken)
        {
            journal.Entries.Add("other");
            return Task.CompletedTask;
        }
    }

    private sealed class BoomException : Exception;

    private sealed class ThrowingThingHandler : IDomainEventHandler<ThingHappened>
    {
        public Task HandleAsync(ThingHappened domainEvent, CancellationToken cancellationToken) =>
            throw new BoomException();
    }

    private static ServiceProvider BuildProvider(
        Journal journal,
        Action<IServiceCollection> configure
    )
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(journal);
        services.AddApplication();
        configure(services);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task DispatchAsync_invokes_every_handler_for_the_event_type_in_registration_order()
    {
        // Arrange — two modules each handling the same event
        var cancellationToken = TestContext.Current.CancellationToken;
        var journal = new Journal();
        await using var provider = BuildProvider(
            journal,
            services =>
            {
                services.AddDomainEventHandler<FirstThingHandler, ThingHappened>();
                services.AddDomainEventHandler<SecondThingHandler, ThingHappened>();
            }
        );
        var dispatcher = provider.GetRequiredService<IDomainEventDispatcher>();

        // Act
        await dispatcher.DispatchAsync([new ThingHappened()], cancellationToken);

        // Assert
        journal.Entries.Should().Equal("first", "second");
    }

    [Fact]
    public async Task DispatchAsync_ignores_an_event_with_no_handlers()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var journal = new Journal();
        await using var provider = BuildProvider(
            journal,
            services => services.AddDomainEventHandler<FirstThingHandler, ThingHappened>()
        );
        var dispatcher = provider.GetRequiredService<IDomainEventDispatcher>();

        // Act
        await dispatcher.DispatchAsync([new UnhandledThingHappened()], cancellationToken);

        // Assert
        journal.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task DispatchAsync_dispatches_each_event_in_the_order_given()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var journal = new Journal();
        await using var provider = BuildProvider(
            journal,
            services =>
            {
                services.AddDomainEventHandler<FirstThingHandler, ThingHappened>();
                services.AddDomainEventHandler<OtherThingHandler, OtherThingHappened>();
            }
        );
        var dispatcher = provider.GetRequiredService<IDomainEventDispatcher>();

        // Act
        await dispatcher.DispatchAsync(
            [new OtherThingHappened(), new ThingHappened()],
            cancellationToken
        );

        // Assert
        journal.Entries.Should().Equal("other", "first");
    }

    [Fact]
    public async Task DispatchAsync_propagates_a_handler_exception_unwrapped()
    {
        // Arrange — a reaction that faults must roll the caller's transaction back (ADR-0016),
        // so the original exception has to surface, not a reflection wrapper.
        var cancellationToken = TestContext.Current.CancellationToken;
        var journal = new Journal();
        await using var provider = BuildProvider(
            journal,
            services => services.AddDomainEventHandler<ThrowingThingHandler, ThingHappened>()
        );
        var dispatcher = provider.GetRequiredService<IDomainEventDispatcher>();

        // Act
        var dispatch = async () =>
            await dispatcher.DispatchAsync([new ThingHappened()], cancellationToken);

        // Assert
        await dispatch.Should().ThrowAsync<BoomException>();
    }
}
