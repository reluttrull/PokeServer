using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PokeServer
{
    public class StringConverter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Convert both string and number to string
            return reader.TokenType switch
            {
                JsonTokenType.String => reader.GetString(),
                JsonTokenType.Number => reader.GetInt32().ToString(), // Or use GetDouble() if needed
                _ => throw new JsonException("Unexpected token type")
            };
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }
    }
}
