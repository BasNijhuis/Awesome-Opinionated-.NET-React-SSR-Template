using System.Collections.Immutable;
using Acme.CQRS.Abstractions;
using Acme.DomainAbstractions;

namespace Acme.CQRS;

/// <summary>
/// Runs a request's validators and projects their failures onto <see cref="Error"/>s. Lives apart
/// from the dispatcher because only the handler adapter knows the request type statically.
/// </summary>
internal static class ValidationRunner
{
    public static ImmutableArray<Error> Run<TRequest>(
        TRequest request,
        IEnumerable<IRequestValidator<TRequest>> validators
    )
    {
        var errors = new List<Error>();
        foreach (var validator in validators)
        {
            var result = validator.Validate(request);
            if (result.IsValid)
            {
                continue;
            }

            errors.AddRange(result.Errors.Select(e => Error.Validation(e.PropertyName, e.Message)));
        }

        return [.. errors];
    }
}
