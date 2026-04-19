using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nexus.Models.Chat
{
    public class ExtraDataJsonConverter : JsonConverter<object>
    {
        public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Null:
                    return null;

                case JsonTokenType.String:
                    var stringValue = reader.GetString();
                    if (string.IsNullOrWhiteSpace(stringValue))
                        return null;
                    
                    try
                    {
                        return JsonSerializer.Deserialize<Dictionary<string, object>>(stringValue, options);
                    }
                    catch (Exception)
                    {
                        return new Dictionary<string, object>
                        {
                            { "raw", stringValue }
                        };
                    }

                case JsonTokenType.StartObject:
                    try
                    {
                        using var jsonDoc = JsonDocument.ParseValue(ref reader);
                        return JsonSerializer.Deserialize<Dictionary<string, object>>(jsonDoc.RootElement.GetRawText(), options);
                    }
                    catch (Exception)
                    {
                        return new Dictionary<string, object>();
                    }

                case JsonTokenType.StartArray:
                case JsonTokenType.Number:
                case JsonTokenType.True:
                case JsonTokenType.False:
                    {
                        using var doc = JsonDocument.ParseValue(ref reader);
                        return new Dictionary<string, object>
                        {
                            { "value", doc.RootElement.ToString() }
                        };
                    }

                default:
                    reader.Skip();
                    return null;
            }
        }

        public override void Write(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            switch (value)
            {
                case Dictionary<string, object> dict:
                    JsonSerializer.Serialize(writer, dict, options);
                    break;
                
                case string str:
                    writer.WriteStringValue(str);
                    break;
                
                default:
                    JsonSerializer.Serialize(writer, value, options);
                    break;
            }
        }
    }
}
