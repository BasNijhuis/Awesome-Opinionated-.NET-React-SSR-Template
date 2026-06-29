using System.Collections.Immutable;

namespace Acme.DomainAbstractions;

/// <summary>
/// Outcome of an operation that can fail in an expected way. A failure carries one or more
/// <see cref="Error"/>s (multiple only for validation field maps). Use <see cref="Result{T}"/>
/// when the success path produces a value.
/// </summary>
public record Result
{
    private protected Result(bool isSuccess, ImmutableArray<Error> errors)
    {
        if (isSuccess && errors.Length > 0)
        {
            throw new InvalidOperationException("A successful result cannot carry errors.");
        }

        if (!isSuccess && errors.Length == 0)
        {
            throw new InvalidOperationException("A failed result must carry at least one error.");
        }

        IsSuccess = isSuccess;
        Errors = errors;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public ImmutableArray<Error> Errors { get; }

    /// <summary>The first error. Only valid on a failed result.</summary>
    public Error Error =>
        IsFailure
            ? Errors[0]
            : throw new InvalidOperationException("A successful result has no error.");

    public static Result Success() => new(true, []);

    public static Result Failure(Error error) => new(false, [error]);

    public static Result Failure(ImmutableArray<Error> errors) => new(false, errors);

    public static Result<T> Success<T>(T value) => Result<T>.Success(value);

    public static Result<T> Failure<T>(Error error) => Result<T>.Failure(error);

    public static Result<T> Failure<T>(ImmutableArray<Error> errors) => Result<T>.Failure(errors);
}

/// <summary>A <see cref="Result"/> whose success path carries a <typeparamref name="T"/> value.</summary>
public sealed record Result<T> : Result
{
    private readonly T _value;

    private Result(bool isSuccess, T value, ImmutableArray<Error> errors)
        : base(isSuccess, errors)
    {
        _value = value;
    }

    /// <summary>The success value. Throws if the result is a failure (a programmer error).</summary>
    public T Value =>
        IsSuccess
            ? _value
            : throw new InvalidOperationException("Cannot access the value of a failed result.");

    public static Result<T> Success(T value) => new(true, value, []);

    public static new Result<T> Failure(Error error) => new(false, default!, [error]);

    public static new Result<T> Failure(ImmutableArray<Error> errors) =>
        new(false, default!, errors);

    /// <summary>Lifts a value into a successful result, so a handler can <c>return value;</c>.</summary>
    public static implicit operator Result<T>(T value) => Success(value);

    /// <summary>Lifts an error into a failed result, so a handler can <c>return someError;</c>.</summary>
    public static implicit operator Result<T>(Error error) => Failure(error);

    /// <summary>
    /// Lifts a set of errors into a failed result, so a handler can propagate a failed result's
    /// <see cref="Result.Errors"/> with <c>return other.Errors;</c>. Typed as
    /// <see cref="ImmutableArray{T}"/> (the shape of <see cref="Result.Errors"/>) because C# forbids
    /// user-defined conversions from an interface (CS0552) and a mutable array is banned in the domain
    /// (ACME003).
    /// </summary>
    public static implicit operator Result<T>(ImmutableArray<Error> errors) => Failure(errors);
}
