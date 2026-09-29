using System.Text.Json;
using System.Text.Json.Serialization;

namespace kingsightapi.Configuration;

/// <summary>
/// Serializes <see cref="long"/> as a JSON string so Fabric BIGINT IDENTITY values
/// are not truncated in JavaScript (Number.MAX_SAFE_INTEGER is 2^53 - 1).
/// </summary>
public sealed class LongAsStringJsonConverter : JsonConverter<long>
{
    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var text = reader.GetString();
            if (long.TryParse(text, out var parsed))
            {
                return parsed;
            }

            throw new JsonException($"Value '{text}' is not a valid 64-bit integer.");
        }

        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetInt64();
        }

        throw new JsonException($"Unexpected token {reader.TokenType} when parsing a long.");
    }

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
