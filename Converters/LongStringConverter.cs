using System.Text.Json;
using System.Text.Json.Serialization;

namespace Netcorext.Extensions.Json.Converters;

public class LongStringConverter : JsonConverter<long>
{
    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                var content = reader.GetString();

                if (long.TryParse(content, out var val)) return val;

                break;
            case JsonTokenType.Number:
                if (reader.TryGetInt64(out var num)) return num;

                break;
        }
        
        throw new NotSupportedException($"Argument format not supported.");
    }

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}