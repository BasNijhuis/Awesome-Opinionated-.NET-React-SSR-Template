using System.Text.Json;
using System.Text.Json.Serialization;

namespace Acme.Kernel.Application.Common;

/// <summary>
/// Shared serialization contract for any value a module persists as a <c>jsonb</c> column. Used by
/// both the write path (the Infrastructure persistence mapper) and the read path (query projections)
/// so the two always agree on the JSON shape.
/// </summary>
public static class PersistenceJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };
}
