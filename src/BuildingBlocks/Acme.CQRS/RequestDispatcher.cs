using System.Collections;
using System.Collections.Immutable;
using System.Reflection;
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
    public Task<Result<TResult>> SendAsync<TResult>(
        ICommand<TResult> command,
        CancellationToken cancellationToken = default
    ) => DispatchAsync<TResult>(command, typeof(ICommandHandler<,>), cancellationToken);

    public Task<Result<TResult>> QueryAsync<TResult>(
        IQuery<TResult> query,
        CancellationToken cancellationToken = default
    ) => DispatchAsync<TResult>(query, typeof(IQueryHandler<,>), cancellationToken);

    private async Task<Result<TResult>> DispatchAsync<TResult>(
        object request,
        Type handlerOpenType,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestType = request.GetType();

        var validationErrors = Validate(request, requestType);
        if (validationErrors.Length > 0)
        {
            LogFailure(requestType, validationErrors);
            return validationErrors;
        }

        var handlerType = handlerOpenType.MakeGenericType(requestType, typeof(TResult));
        var handler = serviceProvider.GetRequiredService(handlerType);

        var handleMethod = handlerType.GetMethod(
            "HandleAsync",
            BindingFlags.Public | BindingFlags.Instance
        );

        if (handleMethod is null)
        {
            throw new InvalidOperationException(
                $"Handler type {handlerType.Name} does not implement HandleAsync."
            );
        }

        if (
            handleMethod.Invoke(handler, [request, cancellationToken])
            is not Task<Result<TResult>> typedTask
        )
        {
            throw new InvalidOperationException(
                $"Handler for {requestType.Name} did not return Task<Result<{typeof(TResult).Name}>>."
            );
        }

        var result = await typedTask.ConfigureAwait(false);
        if (result.IsFailure)
        {
            LogFailure(requestType, result.Errors);
        }

        return result;
    }

    // Centralized observability for expected (Result) failures across every command/query. Logged at
    // Warning, not Error — these are recoverable control-flow failures (ADR-0013), not exceptions.
    private void LogFailure(Type requestType, ImmutableArray<Error> errors) =>
        logger.LogWarning(
            "{Request} failed: {Errors}",
            requestType.Name,
            string.Join("; ", errors.Select(e => $"{e.Category}/{e.Code}: {e.Message}"))
        );

    private ImmutableArray<Error> Validate(object request, Type requestType)
    {
        var validatorType = typeof(IRequestValidator<>).MakeGenericType(requestType);
        var validators = (IEnumerable)serviceProvider.GetServices(validatorType);

        var errors = new List<Error>();
        foreach (var validator in validators)
        {
            if (validator is null)
            {
                continue;
            }

            var validateMethod = validatorType.GetMethod(
                "Validate",
                BindingFlags.Public | BindingFlags.Instance
            );

            if (
                validateMethod?.Invoke(validator, [request]) is not ValidationResult result
                || result.IsValid
            )
            {
                continue;
            }

            errors.AddRange(result.Errors.Select(e => Error.Validation(e.PropertyName, e.Message)));
        }

        return [.. errors];
    }
}
