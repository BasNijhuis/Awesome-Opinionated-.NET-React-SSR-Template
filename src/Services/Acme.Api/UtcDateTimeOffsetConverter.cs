using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Acme.Api;

/// <summary>
/// Serializes <see cref="DateTimeOffset"/> values in canonical UTC form with a trailing <c>Z</c>
/// (e.g. <c>2026-06-22T19:30:00.0000000Z</c>) instead of System.Text.Json's default numeric offset
/// (<c>…+00:00</c>). The generated frontend contract validates date-times as strict UTC (Zod's
/// <c>.datetime()</c>), which rejects the offset form — so an unconverted <c>completedAt</c> made the
/// SSR boundary reject every session response once a round had completed (#12).
/// </summary>
public sealed class UtcDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    ) => reader.GetDateTimeOffset();

    public override void Write(
        Utf8JsonWriter writer,
        DateTimeOffset value,
        JsonSerializerOptions options
    ) => writer.WriteStringValue(value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
}
