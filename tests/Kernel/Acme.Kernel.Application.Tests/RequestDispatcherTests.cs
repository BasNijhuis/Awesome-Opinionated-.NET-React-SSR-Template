using Acme.CQRS;
using Acme.CQRS.Abstractions;
using Acme.DomainAbstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Acme.Kernel.Application.Tests;

/// <summary>
/// Handler declared at namespace scope so its accessibility mirrors a real module handler
/// (internal, per ADR-0014) rather than the private nested types the other tests use.
/// </summary>
internal sealed record InternalPingQuery : IQuery<string>;

internal sealed class InternalPingHandler : IQueryHandler<InternalPingQuery, string>
{
    public Task<Result<string>> HandleAsync(
        InternalPingQuery query,
        CancellationToken cancellationToken
    ) => Task.FromResult(Result.Success("internal pong"));
}

public sealed class RequestDispatcherTests
{
    private sealed record PingQuery : IQuery<string>;

    private sealed class PingHandler : IQueryHandler<PingQuery, string>
    {
        public Task<Result<string>> HandleAsync(
            PingQuery query,
            CancellationToken cancellationToken
        ) => Task.FromResult(Result.Success("pong"));
    }

    private sealed class PingValidator : RequestValidator<PingQuery>
    {
        protected override void Validate(PingQuery request, List<ValidationError> errors) =>
            Rule(false, errors, nameof(PingQuery), "always fails for test");
    }

    private sealed class SecondPingValidator : RequestValidator<PingQuery>
    {
        protected override void Validate(PingQuery request, List<ValidationError> errors) =>
            Rule(false, errors, "Other", "also always fails for test");
    }

    private sealed record CountCommand(int Amount) : ICommand<int>;

    private sealed class CountHandler : ICommandHandler<CountCommand, int>
    {
        public Task<Result<int>> HandleAsync(
            CountCommand command,
            CancellationToken cancellationToken
        ) => Task.FromResult(Result.Success(command.Amount + 1));
    }

    private sealed record MissingQuery : IQuery<string>;

    private sealed record FailingQuery : IQuery<string>;

    private sealed class FailingHandler : IQueryHandler<FailingQuery, string>
    {
        public static readonly Error NotFound = Error.NotFound("Thing.NotFound", "no such thing");

        public Task<Result<string>> HandleAsync(
            FailingQuery query,
            CancellationToken cancellationToken
        ) => Task.FromResult(Result<string>.Failure(NotFound));
    }

    /// <summary>Records whether the handler ran, so a short-circuit can be asserted.</summary>
    private sealed class HandlerProbe
    {
        public bool Invoked { get; set; }
    }

    private sealed class UnconstructableException : Exception;

    /// <summary>Throws on construction, so "was this handler built?" is observable.</summary>
    private sealed class UnconstructableHandler : IQueryHandler<PingQuery, string>
    {
        public UnconstructableHandler() => throw new UnconstructableException();

        public Task<Result<string>> HandleAsync(
            PingQuery query,
            CancellationToken cancellationToken
        ) => Task.FromResult(Result.Success("unreachable"));
    }

    private sealed class ProbedPingHandler(HandlerProbe probe) : IQueryHandler<PingQuery, string>
    {
        public Task<Result<string>> HandleAsync(
            PingQuery query,
            CancellationToken cancellationToken
        )
        {
            probe.Invoked = true;
            return Task.FromResult(Result.Success("pong"));
        }
    }

    private static ServiceProvider BuildProvider(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddApplication();
        configure(services);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task QueryAsync_routes_to_registered_handler()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = BuildProvider(services =>
            services.AddQueryHandler<PingHandler, PingQuery, string>()
        );
        var dispatcher = provider.GetRequiredService<IRequestDispatcher>();

        // Act
        var result = await dispatcher.QueryAsync(new PingQuery(), cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("pong");
    }

    [Fact]
    public async Task SendAsync_routes_to_registered_handler()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = BuildProvider(services =>
            services.AddCommandHandler<CountHandler, CountCommand, int>()
        );
        var dispatcher = provider.GetRequiredService<IRequestDispatcher>();

        // Act
        var result = await dispatcher.SendAsync(new CountCommand(41), cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public async Task QueryAsync_routes_to_an_internal_handler_type()
    {
        // Arrange — an internal request type is a non-public generic argument for the dispatch adapter
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = BuildProvider(services =>
            services.AddQueryHandler<InternalPingHandler, InternalPingQuery, string>()
        );
        var dispatcher = provider.GetRequiredService<IRequestDispatcher>();

        // Act
        var result = await dispatcher.QueryAsync(new InternalPingQuery(), cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("internal pong");
    }

    [Fact]
    public async Task QueryAsync_runs_validators_before_handler()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var probe = new HandlerProbe();
        await using var provider = BuildProvider(services =>
        {
            services.AddSingleton(probe);
            services.AddQueryHandler<ProbedPingHandler, PingQuery, string>();
            services.AddScoped<IRequestValidator<PingQuery>, PingValidator>();
        });
        var dispatcher = provider.GetRequiredService<IRequestDispatcher>();

        // Act
        var result = await dispatcher.QueryAsync(new PingQuery(), cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Category.Should().Be(ErrorCategory.Validation);
        result.Error.Message.Should().Be("always fails for test");
        probe.Invoked.Should().BeFalse("validation short-circuits before the handler runs");
    }

    [Fact]
    public async Task QueryAsync_aggregates_errors_from_every_validator()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = BuildProvider(services =>
        {
            services.AddQueryHandler<PingHandler, PingQuery, string>();
            services.AddScoped<IRequestValidator<PingQuery>, PingValidator>();
            services.AddScoped<IRequestValidator<PingQuery>, SecondPingValidator>();
        });
        var dispatcher = provider.GetRequiredService<IRequestDispatcher>();

        // Act
        var result = await dispatcher.QueryAsync(new PingQuery(), cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Select(error => error.Code).Should().Equal(nameof(PingQuery), "Other");
    }

    [Fact]
    public async Task QueryAsync_returns_the_handlers_failure_result()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = BuildProvider(services =>
            services.AddQueryHandler<FailingHandler, FailingQuery, string>()
        );
        var dispatcher = provider.GetRequiredService<IRequestDispatcher>();

        // Act
        var result = await dispatcher.QueryAsync(new FailingQuery(), cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(FailingHandler.NotFound);
    }

    [Fact]
    public async Task QueryAsync_throws_when_no_handler_is_registered()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = BuildProvider(_ => { });
        var dispatcher = provider.GetRequiredService<IRequestDispatcher>();

        // Act
        var dispatch = async () =>
            await dispatcher.QueryAsync(new MissingQuery(), cancellationToken);

        // Assert — the message must name the request and the fix, not the internal adapter type
        (await dispatch.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should()
            .Contain(nameof(MissingQuery))
            .And.Contain(nameof(Acme.CQRS.DependencyInjection.AddQueryHandler));
    }

    [Fact]
    public async Task QueryAsync_does_not_build_the_handler_when_validation_fails()
    {
        // Arrange — an invalid request must not pay for the handler's dependency graph
        // (repositories, DbContexts, connections), nor turn a construction fault into a 500.
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = BuildProvider(services =>
        {
            services.AddQueryHandler<UnconstructableHandler, PingQuery, string>();
            services.AddScoped<IRequestValidator<PingQuery>, PingValidator>();
        });
        var dispatcher = provider.GetRequiredService<IRequestDispatcher>();

        // Act
        var result = await dispatcher.QueryAsync(new PingQuery(), cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Category.Should().Be(ErrorCategory.Validation);
    }

    [Fact]
    public async Task QueryAsync_reports_a_missing_handler_through_the_returned_task()
    {
        // Arrange — the task is stored before being awaited, as a Task.WhenAll caller does; a
        // synchronous throw would escape the caller's try/catch around the await.
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = BuildProvider(_ => { });
        var dispatcher = provider.GetRequiredService<IRequestDispatcher>();

        // Act
        var dispatch = dispatcher.QueryAsync(new MissingQuery(), cancellationToken);

        // Assert
        var awaiting = async () => await dispatch;
        await awaiting.Should().ThrowAsync<InvalidOperationException>();
    }
}
