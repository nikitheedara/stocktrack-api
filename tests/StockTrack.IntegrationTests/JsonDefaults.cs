using System.Text.Json;
using System.Text.Json.Serialization;

namespace StockTrack.IntegrationTests;

internal static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}
