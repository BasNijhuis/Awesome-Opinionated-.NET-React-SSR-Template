namespace Acme.CQRS.Abstractions;

public sealed class ValidationException : Exception
{
    public ValidationException(IReadOnlyList<ValidationError> errors)
        : base(BuildMessage(errors))
    {
        Errors = errors;
    }

    public IReadOnlyList<ValidationError> Errors { get; }

    private static string BuildMessage(IReadOnlyList<ValidationError> errors) =>
        errors.Count == 1 ? errors[0].Message : $"{errors.Count} validation errors occurred.";
}
