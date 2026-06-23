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

    public static Greeting Create(ICreateGreetingSpec spec)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(spec.Message);
        return new Greeting(GreetingId.New(), spec.Message.Trim(), DateTimeOffset.UtcNow);
    }

    public static Greeting Rehydrate(IGreetingState state) =>
        new(state.Id, state.Message, state.CreatedAt);
}
