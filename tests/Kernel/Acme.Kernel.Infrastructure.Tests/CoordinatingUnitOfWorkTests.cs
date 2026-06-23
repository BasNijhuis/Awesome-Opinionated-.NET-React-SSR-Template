using System.Data.Common;
using Acme.CQRS.Abstractions;
using Acme.DomainAbstractions;
using Acme.Kernel.Infrastructure.Persistence;
using Acme.Modules.Greetings.Domain;
using Acme.Modules.Greetings.Infrastructure.Persistence;
using Acme.Modules.Widgets.Domain;
using Acme.Modules.Widgets.Infrastructure.Persistence;
using Acme.TestSupport;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Acme.Kernel.Infrastructure.Tests;

/// <summary>
/// Proves the coordinating <see cref="AcmeUnitOfWork"/> commits every module write context in a
/// single transaction and rolls them all back on failure. The Greetings and Widgets modules own
/// separate schemas but share one connection; the unit of work must make their writes atomic.
/// </summary>
public sealed class CoordinatingUnitOfWorkTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = PostgresTestContainer.Create();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        // Migrate both module schemas onto the shared database up front.
        await using var connection = CreateConnection();
        await using var greetingsDb = CreateGreetingsContext(connection);
        await greetingsDb.Database.MigrateAsync();
        await using var widgetsDb = CreateWidgetsContext(connection);
        await widgetsDb.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task Commits_greeting_and_widget_in_one_transaction()
    {
        // Arrange — a greeting (Greetings schema) and a widget (Widgets schema) staged across the two
        // write contexts under one unit of work.
        var greeting = Greeting.Create(new CreateGreetingSpec("hello"));
        var widget = Widget.Create(new CreateWidgetSpec("Gadget", 7));

        await using (var connection = CreateConnection())
        await using (var greetingsDb = CreateGreetingsContext(connection))
        await using (var widgetsDb = CreateWidgetsContext(connection))
        {
            var tracked = new TrackedAggregates();
            new EfGreetingRepository(greetingsDb, tracked).Add(greeting);
            new EfWidgetRepository(widgetsDb, tracked).Add(widget);

            // Act — one unit of work over both participants commits both schemas together.
            await CreateUnitOfWork(
                    connection,
                    tracked,
                    new GreetingTransactionalParticipant(greetingsDb),
                    new WidgetTransactionalParticipant(widgetsDb)
                )
                .SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Assert — both rows survived, each in its own schema, read back in fresh scopes.
        await using var verifyConnection = CreateConnection();
        await using var verifyGreetingsDb = CreateGreetingsContext(verifyConnection);
        var loadedGreeting = await new EfGreetingRepository(
            verifyGreetingsDb,
            new TrackedAggregates()
        ).GetByIdAsync(greeting.Id, TestContext.Current.CancellationToken);
        await using var verifyWidgetsDb = CreateWidgetsContext(verifyConnection);
        var loadedWidget = await new EfWidgetRepository(
            verifyWidgetsDb,
            new TrackedAggregates()
        ).GetByIdAsync(widget.Id, TestContext.Current.CancellationToken);

        loadedGreeting.Should().NotBeNull();
        loadedGreeting!.Message.Should().Be("hello");
        loadedWidget.Should().NotBeNull();
        loadedWidget!.Quantity.Should().Be(7);
    }

    [Fact]
    public async Task Rolls_back_every_participant_when_one_fails()
    {
        // Arrange — a valid greeting staged in the Greetings context, plus a participant that throws on
        // save. The failure must roll back the greeting write too.
        var greeting = Greeting.Create(new CreateGreetingSpec("rollback"));

        await using (var connection = CreateConnection())
        await using (var greetingsDb = CreateGreetingsContext(connection))
        {
            var tracked = new TrackedAggregates();
            new EfGreetingRepository(greetingsDb, tracked).Add(greeting);
            var unitOfWork = CreateUnitOfWork(
                connection,
                tracked,
                new GreetingTransactionalParticipant(greetingsDb),
                new ThrowingParticipant()
            );

            // Act
            var save = async () =>
                await unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Assert — the failure propagates.
            await save.Should().ThrowAsync<InvalidOperationException>();
        }

        // Assert — the greeting write was rolled back alongside the failing participant.
        await using var verifyConnection = CreateConnection();
        await using var verifyDb = CreateGreetingsContext(verifyConnection);
        var loaded = await new EfGreetingRepository(verifyDb, new TrackedAggregates()).GetByIdAsync(
            greeting.Id,
            TestContext.Current.CancellationToken
        );

        loaded.Should().BeNull();
    }

    private NpgsqlConnection CreateConnection() => new(_postgres.GetConnectionString());

    private static GreetingsDbContext CreateGreetingsContext(NpgsqlConnection connection) =>
        new(new DbContextOptionsBuilder<GreetingsDbContext>().UseNpgsql(connection).Options);

    private static WidgetsDbContext CreateWidgetsContext(NpgsqlConnection connection) =>
        new(new DbContextOptionsBuilder<WidgetsDbContext>().UseNpgsql(connection).Options);

    private static AcmeUnitOfWork CreateUnitOfWork(
        NpgsqlConnection connection,
        TrackedAggregates tracked,
        params ITransactionalParticipant[] participants
    ) => new(connection, participants, tracked, NoEvents);

    // These persistence tests don't raise domain events; a no-op dispatcher is enough.
    private static readonly IDomainEventDispatcher NoEvents = new NoOpDomainEventDispatcher();

    private sealed class NoOpDomainEventDispatcher : IDomainEventDispatcher
    {
        public Task DispatchAsync(
            IEnumerable<IDomainEvent> domainEvents,
            CancellationToken cancellationToken
        ) => Task.CompletedTask;
    }

    private sealed class ThrowingParticipant : ITransactionalParticipant
    {
        public Task EnlistAsync(DbTransaction transaction, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Simulated participant failure.");
    }
}
