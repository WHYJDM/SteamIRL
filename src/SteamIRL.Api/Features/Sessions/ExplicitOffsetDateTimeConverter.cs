using System.Text.Json;
using System.Text.Json.Serialization;

namespace SteamIRL.Api.Features.Sessions;

public sealed class ExplicitOffsetDateTimeConverter : JsonConverter<DateTimeOffset>
{
    public static bool HasOffset(string value) => value.EndsWith('Z') || value.Length >= 6
        && value[^3] == ':' && value[^6] is '+' or '-';
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String) throw new JsonException("Expected an ISO timestamp string.");
        var value = reader.GetString();
        if (value is null || !HasOffset(value) || !reader.TryGetDateTimeOffset(out var parsed))
            throw new JsonException("Use an ISO timestamp with Z or an explicit UTC offset.");
        return parsed;
    }
    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) => writer.WriteStringValue(value);
}
