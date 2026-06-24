using Acme.CQRS.Abstractions;
using Acme.DomainAbstractions;
using Acme.Kernel.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Acme.Kernel.Application.Tests;

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

    [Fact]
    public async Task QueryAsync_routes_to_registered_handler()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddApplication();
        services.AddScoped<IQueryHandler<PingQuery, string>, PingHandler>();
        await using var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IRequestDispatcher>();

        // Act
        var result = await dispatcher.QueryAsync(new PingQuery(), cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("pong");
    }

    [Fact]
    public async Task QueryAsync_runs_validators_before_handler()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddApplication();
        services.AddScoped<IQueryHandler<PingQuery, string>, PingHandler>();
        services.AddScoped<IRequestValidator<PingQuery>, PingValidator>();
        await using var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IRequestDispatcher>();

        // Act
        var result = await dispatcher.QueryAsync(new PingQuery(), cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Category.Should().Be(ErrorCategory.Validation);
        result.Error.Message.Should().Be("always fails for test");
    }
}
