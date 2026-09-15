using System.Collections.Immutable;
using Acme.CQRS.Abstractions;
using Acme.DomainAbstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Acme.CQRS;

/// <summary>
/// Dispatch-side view of a request handler, closed over the success type only. The dispatcher knows
/// <typeparamref name="TResult"/> statically but learns the request type at runtime, so it resolves
/// the adapter keyed by that runtime type; the adapter is where the request type <em>is</em> static,
/// which is what lets <c>HandleAsync</c> be a plain virtual call instead of a reflective one
/// (ADR-0019, amending ADR-0013).
/// </summary>
internal interface IRequestHandlerAdapter<TResult>
{
    ImmutableArray<Error> Validate(object request);

    Task<Result<TResult>> HandleAsync(object request, CancellationToken cancellationToken);
}

/// <summary>Reached by <see cref="IRequestDispatcher.SendAsync"/> only, so a query cannot be sent as a command.</summary>
internal interface ICommandHandlerAdapter<TResult> : IRequestHandlerAdapter<TResult>;

/// <summary>Reached by <see cref="IRequestDispatcher.QueryAsync"/> only.</summary>
internal interface IQueryHandlerAdapter<TResult> : IRequestHandlerAdapter<TResult>;

/// <summary>
/// Binds <typeparamref name="TCommand"/> to its handler and validators. The casts are safe by
/// construction: the adapter is only ever resolved under the key <c>request.GetType()</c>.
/// The handler is resolved on use, not injected, so a request that fails validation never builds the
/// handler's dependency graph (repositories, DbContexts, connections) — matching the pre-ADR-0019
/// ordering. <see cref="IServiceProvider"/> here is the request's own scope.
/// </summary>
internal sealed class CommandHandlerAdapter<TCommand, TResult>(
    IServiceProvider serviceProvider,
    IEnumerable<IRequestValidator<TCommand>> validators
) : ICommandHandlerAdapter<TResult>
    where TCommand : ICommand<TResult>
{
    public ImmutableArray<Error> Validate(object request) =>
        ValidationRunner.Run((TCommand)request, validators);

    public Task<Result<TResult>> HandleAsync(object request, CancellationToken cancellationToken) =>
        serviceProvider
            .GetRequiredService<ICommandHandler<TCommand, TResult>>()
            .HandleAsync((TCommand)request, cancellationToken);
}

/// <inheritdoc cref="CommandHandlerAdapter{TCommand,TResult}"/>
internal sealed class QueryHandlerAdapter<TQuery, TResult>(
    IServiceProvider serviceProvider,
    IEnumerable<IRequestValidator<TQuery>> validators
) : IQueryHandlerAdapter<TResult>
    where TQuery : IQuery<TResult>
{
    public ImmutableArray<Error> Validate(object request) =>
        ValidationRunner.Run((TQuery)request, validators);

    public Task<Result<TResult>> HandleAsync(object request, CancellationToken cancellationToken) =>
        serviceProvider
            .GetRequiredService<IQueryHandler<TQuery, TResult>>()
            .HandleAsync((TQuery)request, cancellationToken);
}

/// <summary>
/// Dispatch-side view of every handler registered for one event type. One adapter per event (not per
/// handler) so the fan-out and its ordering stay inside the typed layer.
/// </summary>
internal interface IDomainEventHandlerAdapter
{
    Task HandleAllAsync(IDomainEvent domainEvent, CancellationToken cancellationToken);
}

/// <inheritdoc cref="IDomainEventHandlerAdapter"/>
internal sealed class DomainEventHandlerAdapter<TEvent>(
    IEnumerable<IDomainEventHandler<TEvent>> handlers
) : IDomainEventHandlerAdapter
    where TEvent : IDomainEvent
{
    public async Task HandleAllAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        foreach (var handler in handlers)
        {
            await handler.HandleAsync((TEvent)domainEvent, cancellationToken);
        }
    }
}
