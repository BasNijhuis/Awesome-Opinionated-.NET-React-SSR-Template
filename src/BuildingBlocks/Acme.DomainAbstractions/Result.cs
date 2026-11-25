using System.Collections.Immutable;

namespace Acme.DomainAbstractions;

/// <summary>
/// Outcome of an operation that can fail in an expected way: either a <see cref="Success{T}"/>
/// carrying the value or a <see cref="Failure"/> carrying one or more <see cref="Error"/>s (ADR-0020).
/// Handlers return it with <c>return value;</c>, <c>return error;</c> or <c>return errors;</c>.
/// </summary>
/// <remarks>
/// <para>
/// Consume it with a <c>switch</c> <em>expression</em> over both cases. The compiler checks that it
/// is exhaustive, and a missing case is a build error (CS8509/CS8846). A <c>switch</c>
/// <em>statement</em> is not checked. To continue only after a success, match the success case
/// (<c>if (result is Success&lt;T&gt;(var value))</c>), never <c>is not Failure</c>.
/// </para>
/// <para>
/// Two traps a union brings. The compiler-generated <c>Value</c> property is the <em>case</em>
/// (<see cref="Success{T}"/> or <see cref="Failure"/>) typed as <c>object?</c>, not the success value,
/// so don't read it. <c>default(Result&lt;T&gt;)</c> holds neither case and matches no case pattern.
/// </para>
/// </remarks>
public union Result<T>(Success<T>, Failure)
{
    /// <summary>Lifts a value into a success, so a handler can <c>return value;</c>.</summary>
    public static implicit operator Result<T>(T value) => new Success<T>(value);

    /// <summary>Lifts an error into a failure, so a handler can <c>return someError;</c>.</summary>
    public static implicit operator Result<T>(Error error) => new Failure([error]);

    /// <summary>
    /// Lifts a set of errors into a failure, so a handler can <c>return errors;</c>. Typed as
    /// <see cref="ImmutableArray{T}"/> (the shape of <see cref="Failure.Errors"/>) because C# forbids
    /// user-defined conversions from an interface (CS0552) and a mutable array is banned in the domain
    /// (ACME003).
    /// </summary>
    public static implicit operator Result<T>(ImmutableArray<Error> errors) => new Failure(errors);
}

/// <summary>The success case of a <see cref="Result{T}"/>.</summary>
public sealed record Success<T>(T Value);

/// <summary>
/// The failure case of a <see cref="Result{T}"/>: one or more <see cref="Error"/>s (several only for
/// validation field maps). Created through <see cref="Result{T}"/>'s conversions. A failure without
/// errors is a programmer error and throws.
/// </summary>
public sealed record Failure
{
    internal Failure(ImmutableArray<Error> errors)
    {
        if (errors.IsDefaultOrEmpty)
        {
            throw new InvalidOperationException("A failed result must carry at least one error.");
        }

        Errors = errors;
    }

    public ImmutableArray<Error> Errors { get; }

    /// <summary>The first error; a failure always has one.</summary>
    public Error Error => Errors[0];
}
