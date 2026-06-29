using Acme.DomainAbstractions;
using Acme.Modules.Greetings.Domain.Contracts;

namespace Acme.Modules.Greetings.Domain;

/// <summary>
/// The Greetings capability aggregate: a single message captured at a point in time.
/// <para>Immutable (#23): every transition returns a new <see cref="Greeting"/>.</para>
/// </summary>
public sealed record Greeting : AggregateRoot
{
    public GreetingId Id { get; private init; }
    public string Message { get; private init; }
    public DateTimeOffset CreatedAt { get; private init; }

    public override bool HasSameIdentity(AggregateRoot other) => other is Greeting o && o.Id == Id;

    private Greeting(GreetingId id, string message, DateTimeOffset createdAt)
    {
        Id = id;
        Message = message;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// Creates a greeting from <paramref name="spec"/>. A blank message is an expected, recoverable
    /// failure (a <see cref="ErrorCategory.Validation"/>) returned as a <see cref="Result{T}"/> rather
    /// than thrown; the error code is the field name so it slots into the API's validation field map.
    /// </summary>
    public static Result<Greeting> Create(ICreateGreetingSpec spec)
    {
        if (string.IsNullOrWhiteSpace(spec.Message))
        {
            return Error.Validation(nameof(spec.Message), "Message is required.");
        }

        return new Greeting(GreetingId.New(), spec.Message.Trim(), DateTimeOffset.UtcNow);
    }

    public static Greeting Rehydrate(IGreetingState state) =>
        new(state.Id, state.Message, state.CreatedAt);
}
