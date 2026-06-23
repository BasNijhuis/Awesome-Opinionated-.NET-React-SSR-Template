using System.Text.Json.Serialization;

namespace Acme.Modules.Greetings.Domain.Contracts;

/// <summary>
/// Strongly-typed identity of a greeting aggregate. Backed by a <see cref="Guid"/> generated with
/// <see cref="Guid.CreateVersion7()"/> (time-ordered — index-friendly). Crosses module/aggregate
/// boundaries by value; serializes as a bare GUID string (see <see cref="GreetingIdJsonConverter"/>)
/// so the public wire contract is unchanged.
/// </summary>
[JsonConverter(typeof(GreetingIdJsonConverter))]
public readonly record struct GreetingId(Guid Value)
{
    public static GreetingId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
