using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Netcorext.Extensions.Json.Converters;

public class EnumerableFlattenConverter<T, TProperty> : JsonConverter<T>
{
    private readonly Action<T, Dictionary<string, string?>> _setter;
    private readonly Func<TProperty, (string Key, string? Value)> _getter;

    private const byte QUOTATION = 34;
    private const byte COMMA = 44;
    private const byte COLON = 58;
    private const byte SQUARE_L = 91;
    private const byte SQUARE_R = 93;
    private const byte BRACE_L = 123;
    private const byte BRACE_R = 125;

    public EnumerableFlattenConverter(Action<T, Dictionary<string, string?>> setter, Func<TProperty, (string Key, string? Value)> getter)
    {
        _setter = setter;
        _getter = getter;
    }

    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var properties = typeToConvert.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        var jsonBytes = new List<byte>();

        string propName = null!;

        var unknownProperty = new Dictionary<string, string?>();

        if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
            jsonBytes.AddRange(reader.ValueSpan.ToArray());

        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    if (jsonBytes[^1] is not SQUARE_L and not SQUARE_R and not COLON)
                        jsonBytes.Add(COMMA);

                    jsonBytes.AddRange(reader.ValueSpan.ToArray());

                    break;
                case JsonTokenType.StartArray:
                    if (jsonBytes[^1] is not SQUARE_R and not BRACE_R and not COLON)
                        jsonBytes.Add(COMMA);

                    jsonBytes.AddRange(reader.ValueSpan.ToArray());

                    break;
                case JsonTokenType.EndObject:
                case JsonTokenType.EndArray:
                    jsonBytes.AddRange(reader.ValueSpan.ToArray());

                    break;
                case JsonTokenType.PropertyName:
                    propName = reader.GetString()!;

                    if (jsonBytes[^1] is not SQUARE_L and not BRACE_L)
                        jsonBytes.Add(COMMA);

                    jsonBytes.Add(QUOTATION);
                    jsonBytes.AddRange(reader.ValueSpan.ToArray());
                    jsonBytes.Add(QUOTATION);
                    jsonBytes.Add(COLON);

                    break;
                case JsonTokenType.String:
                    if (reader.CurrentDepth == 1 && !properties.Any(t => t.Name.Equals(propName, StringComparison.OrdinalIgnoreCase)))
                        unknownProperty.Add(propName, Encoding.UTF8.GetString(reader.ValueSpan));

                    jsonBytes.Add(QUOTATION);
                    jsonBytes.AddRange(reader.ValueSpan.ToArray());
                    jsonBytes.Add(QUOTATION);

                    break;
                case JsonTokenType.True:
                case JsonTokenType.False:
                case JsonTokenType.Number:
                    if (reader.CurrentDepth == 1 && !properties.Any(t => t.Name.Equals(propName, StringComparison.OrdinalIgnoreCase)))
                        unknownProperty.Add(propName, Encoding.UTF8.GetString(reader.ValueSpan));

                    jsonBytes.AddRange(reader.ValueSpan.ToArray());

                    break;
                case JsonTokenType.Null:
                    jsonBytes.AddRange(reader.ValueSpan.ToArray());

                    break;
                default:
                    continue;
            }
        }

        var opt = new JsonSerializerOptions
                  {
                      AllowTrailingCommas = options.AllowTrailingCommas,
                      Encoder = options.Encoder,
                      IncludeFields = options.IncludeFields,
                      MaxDepth = options.MaxDepth,
                      NumberHandling = options.NumberHandling,
                      ReferenceHandler = options.ReferenceHandler,
                      WriteIndented = options.WriteIndented,
                      DefaultBufferSize = options.DefaultBufferSize,
                      DefaultIgnoreCondition = options.DefaultIgnoreCondition,
                      DictionaryKeyPolicy = options.DictionaryKeyPolicy,
                      PropertyNamingPolicy = options.PropertyNamingPolicy,
                      ReadCommentHandling = options.ReadCommentHandling,
                      UnknownTypeHandling = options.UnknownTypeHandling,
                      IgnoreReadOnlyFields = options.IgnoreReadOnlyFields,
                      IgnoreReadOnlyProperties = options.IgnoreReadOnlyProperties,
                      PropertyNameCaseInsensitive = options.PropertyNameCaseInsensitive,
                      Converters =
                      {
                          new LongStringConverter()
                      }
                  };
        
        var json = Encoding.UTF8.GetString(jsonBytes.ToArray());
        
        var result = JsonSerializer.Deserialize<T?>(json, opt);

        _setter.Invoke(result!, unknownProperty);

        return result;
    }

    public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();

            return;
        }

        var type = typeof(T);

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        writer.WriteStartObject();

        foreach (var property in properties)
        {
            var val = property.GetValue(value);

            if (val == null) continue;

            if (!typeof(IEnumerable<TProperty>).IsAssignableFrom(property.PropertyType))
            {
                var elem = JsonSerializer.SerializeToNode(val, property.PropertyType, options);

                writer.WritePropertyName(property.Name);

                writer.WriteRawValue(elem!.ToJsonString(options));

                continue;
            }

            foreach (var i in (IEnumerable<TProperty>)val)
            {
                var kv = _getter.Invoke(i);

                writer.WriteString(kv.Key, kv.Value);
            }
        }

        writer.WriteEndObject();
    }
}