namespace Acme.CQRS.Abstractions;

public sealed class ValidationResult
{
    public static ValidationResult Success { get; } = new([]);

    public IReadOnlyList<ValidationError> Errors { get; }

    public bool IsValid => Errors.Count == 0;

    private ValidationResult(IReadOnlyList<ValidationError> errors)
    {
        Errors = errors;
    }

    public static ValidationResult Failure(params ValidationError[] errors) => new(errors);

    public static ValidationResult Combine(params ValidationResult[] results)
    {
        var errors = results.SelectMany(r => r.Errors).ToArray();
        return errors.Length == 0 ? Success : new ValidationResult(errors);
    }
}
