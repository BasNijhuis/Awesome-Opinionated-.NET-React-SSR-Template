using Acme.CQRS;
using Acme.CQRS.Abstractions;
using Acme.DomainAbstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Acme.Kernel.Application.Tests;

/// <summary>
/// A handler registered without its dispatch adapter still resolves from DI, so nothing fails until
/// runtime — and for a domain event it would fail <em>silently</em>, dropping an in-transaction
/// reaction (ADR-0016). These pin the boot-time guard that makes that a startup failure instead.
/// </summary>
public sealed class CqrsRegistrationValidationTests
{
    private sealed record ThingHappened : IDomainEvent;

    private sealed class ThingHandler : IDomainEventHandler<ThingHappened>
    {
        public Task HandleAsync(ThingHappened domainEvent, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed record LooseQuery : IQuery<string>;

    private sealed class LooseQueryHandler : IQueryHandler<LooseQuery, string>
    {
        public Task<Result<string>> HandleAsync(
            LooseQuery query,
            CancellationToken cancellationToken
        ) => Task.FromResult(Result.Success("loose"));
    }

    [Fact]
    public void ValidateCqrsRegistrations_rejects_a_domain_event_handler_without_its_adapter()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddScoped<IDomainEventHandler<ThingHappened>, ThingHandler>();

        // Act
        var validate = services.ValidateCqrsRegistrations;

        // Assert
        validate
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage($"*{nameof(ThingHappened)}*");
    }

    [Fact]
    public void ValidateCqrsRegistrations_rejects_a_query_handler_without_its_adapter()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddScoped<IQueryHandler<LooseQuery, string>, LooseQueryHandler>();

        // Act
        var validate = services.ValidateCqrsRegistrations;

        // Assert
        validate
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage($"*{nameof(LooseQuery)}*");
    }

    [Fact]
    public void ValidateCqrsRegistrations_accepts_handlers_registered_through_the_helpers()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddDomainEventHandler<ThingHandler, ThingHappened>();
        services.AddQueryHandler<LooseQueryHandler, LooseQuery, string>();

        // Act
        var validate = services.ValidateCqrsRegistrations;

        // Assert
        validate.Should().NotThrow();
    }
}
