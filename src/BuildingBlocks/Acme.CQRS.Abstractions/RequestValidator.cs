namespace Acme.CQRS.Abstractions;

public abstract class RequestValidator<TRequest> : IRequestValidator<TRequest>
{
    public ValidationResult Validate(TRequest request)
    {
        var errors = new List<ValidationError>();
        Validate(request, errors);
        return errors.Count == 0 ? ValidationResult.Success : ValidationResult.Failure([.. errors]);
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
