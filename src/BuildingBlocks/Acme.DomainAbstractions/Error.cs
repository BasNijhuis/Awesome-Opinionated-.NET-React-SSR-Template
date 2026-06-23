namespace Acme.DomainAbstractions;

/// <summary>
/// An expected, recoverable failure. <see cref="Code"/> doubles as the field name for
/// <see cref="ErrorCategory.Validation"/> errors so the API can build a field map.
/// </summary>
public sealed record Error
{
    public string Code { get; }
    public string Message { get; }
    public ErrorCategory Category { get; }

    private Error(string code, string message, ErrorCategory category)
    {
        Code = code;
        Message = message;
        Category = category;
    }

    public static Error NotFound(string code, string message) =>
        new(code, message, ErrorCategory.NotFound);

    public static Error Conflict(string code, string message) =>
        new(code, message, ErrorCategory.Conflict);

    public static Error Validation(string code, string message) =>
        new(code, message, ErrorCategory.Validation);
}
