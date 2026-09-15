using System.Collections.Immutable;
using Acme.CQRS.Abstractions;
using Acme.DomainAbstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Acme.CQRS;

public sealed class RequestDispatcher(
    IServiceProvider serviceProvider,
    ILogger<RequestDispatcher> logger
) : IRequestDispatcher
{
    public async Task<Result<TResult>> SendAsync<TResult>(
        ICommand<TResult> command,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var requestType = command.GetType();
        var adapter =
            serviceProvider.GetKeyedService<ICommandHandlerAdapter<TResult>>(requestType)
            ?? throw MissingHandler(requestType, typeof(TResult), isQuery: false);

        return await DispatchAsync(requestType, command, adapter, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Result<TResult>> QueryAsync<TResult>(
        IQuery<TResult> query,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);

        var requestType = query.GetType();
        var adapter =
            serviceProvider.GetKeyedService<IQueryHandlerAdapter<TResult>>(requestType)
            ?? throw MissingHandler(requestType, typeof(TResult), isQuery: true);

        return await DispatchAsync(requestType, query, adapter, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<Result<TResult>> DispatchAsync<TResult>(
        Type requestType,
        object request,
        IRequestHandlerAdapter<TResult> adapter,
        CancellationToken cancellationToken
    )
    {
        var validationErrors = adapter.Validate(request);
        if (validationErrors.Length > 0)
        {
            LogFailure(requestType, validationErrors);
            return validationErrors;
        }

        var result = await adapter.HandleAsync(request, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            LogFailure(requestType, result.Errors);
        }

        return result;
    }

    // DI's own "no keyed service" message names the internal adapter and omits the request type,
    // which is useless for the mistake it usually signals: a handler registered without its adapter.
    private static InvalidOperationException MissingHandler(
        Type requestType,
        Type resultType,
        bool isQuery
    )
    {
        var kind = isQuery ? "query" : "command";
        var register = isQuery
            ? nameof(DependencyInjection.AddQueryHandler)
            : nameof(DependencyInjection.AddCommandHandler);

        return new InvalidOperationException(
            $"No handler is registered for {kind} {requestType.Name} with result type "
                + $"{resultType.Name}. Scan its assembly with AddCqrsHandlersFrom(...), or register it "
                + $"explicitly with {register}<THandler, {requestType.Name}, {resultType.Name}>()."
        );
    }

    // Centralized observability for expected (Result) failures across every command/query. Logged at
    // Warning, not Error — these are recoverable control-flow failures (ADR-0013), not exceptions.
    private void LogFailure(Type requestType, ImmutableArray<Error> errors) =>
        logger.LogWarning(
            "{Request} failed: {Errors}",
            requestType.Name,
            string.Join("; ", errors.Select(e => $"{e.Category}/{e.Code}: {e.Message}"))
        );
}
