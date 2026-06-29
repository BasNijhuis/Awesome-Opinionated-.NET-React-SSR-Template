using System.Collections.Immutable;

namespace Acme.CQRS.Abstractions;

public sealed class ValidationResult
{
    public static ValidationResult Success { get; } = new([]);

    public ImmutableArray<ValidationError> Errors { get; }

    public bool IsValid => Errors.IsEmpty;

    private ValidationResult(ImmutableArray<ValidationError> errors)
    {
        Errors = errors;
    }

    public static ValidationResult Failure(params IEnumerable<ValidationError> errors) =>
        new([.. errors]);

    public static ValidationResult Combine(params IEnumerable<ValidationResult> results)
    {
        var errors = results.SelectMany(r => r.Errors).ToImmutableArray();
        return errors.IsEmpty ? Success : errors;
    }

    /// <summary>
    /// Lifts validation errors into a failed result, so a validator can <c>return errors;</c> instead
    /// of <c>ValidationResult.Failure(...)</c>. Typed as <see cref="ImmutableArray{T}"/> (the shape of
    /// <see cref="Errors"/>) because C# forbids user-defined conversions from an interface (CS0552).
    /// </summary>
    public static implicit operator ValidationResult(ImmutableArray<ValidationError> errors) =>
        Failure(errors);
}
