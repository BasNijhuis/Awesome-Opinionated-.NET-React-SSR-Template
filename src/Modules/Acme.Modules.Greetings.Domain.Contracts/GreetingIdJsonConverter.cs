using System.Text.Json;
using System.Text.Json.Serialization;

namespace Acme.Modules.Greetings.Domain.Contracts;

/// <summary>(De)serializes <see cref="GreetingId"/> as a bare GUID string, so the wire contract is unchanged.</summary>
public sealed class GreetingIdJsonConverter : JsonConverter<GreetingId>
{
    public override GreetingId Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    ) => new(reader.GetGuid());

    public override void Write(
        Utf8JsonWriter writer,
        GreetingId value,
        JsonSerializerOptions options
    ) => writer.WriteStringValue(value.Value);
}
