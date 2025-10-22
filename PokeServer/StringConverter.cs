using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PokeServer
{
    // we need this for fields that can come back as either string or number from the tcgdex API (e.g., "damage" in attacks)
    public class StringConverter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Convert both string and number to string
            return reader.TokenType switch
            {
                JsonTokenType.String => reader.GetString(),
                JsonTokenType.Number => reader.GetInt32().ToString(),
                _ => throw new JsonException("Unexpected token type")
            };
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }
    }
}
