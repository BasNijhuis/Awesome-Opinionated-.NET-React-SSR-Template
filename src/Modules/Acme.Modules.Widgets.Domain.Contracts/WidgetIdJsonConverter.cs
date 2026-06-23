using System.Text.Json;
using System.Text.Json.Serialization;

namespace Acme.Modules.Widgets.Domain.Contracts;

/// <summary>(De)serializes <see cref="WidgetId"/> as a bare GUID string, so the wire contract is unchanged.</summary>
public sealed class WidgetIdJsonConverter : JsonConverter<WidgetId>
{
    public override WidgetId Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    ) => new(reader.GetGuid());

    public override void Write(
        Utf8JsonWriter writer,
        WidgetId value,
        JsonSerializerOptions options
    ) => writer.WriteStringValue(value.Value);
}
