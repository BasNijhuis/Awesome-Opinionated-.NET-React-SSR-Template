using System.Collections.Immutable;

namespace Acme.CQRS.Abstractions;

public abstract class RequestValidator<TRequest> : IRequestValidator<TRequest>
{
    public ValidationResult Validate(TRequest request)
    {
        var errors = new List<ValidationError>();
        Validate(request, errors);
        return errors.Count == 0 ? ValidationResult.Success : errors.ToImmutableArray();
    }

    protected abstract void Validate(TRequest request, List<ValidationError> errors);

    protected static void Rule(
        bool condition,
        List<ValidationError> errors,
        string propertyName,
        string message
    )
    {
        if (!condition)
        {
            errors.Add(new ValidationError { PropertyName = propertyName, Message = message });
        }
    }
}
