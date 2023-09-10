using System.Dynamic;
using Newtonsoft.Json;

namespace Musoq.DataSources.JsonHelpers;

public static class JsonParser
{
    public static IEnumerable<ExpandoObject> ParseArray(JsonTextReader reader, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var result = new List<ExpandoObject>();
        while (reader.Read() && reader.TokenType != JsonToken.EndArray)
        {
            if (reader.TokenType == JsonToken.StartObject)
            {
                result.Add(ParseObject(reader, cancellationToken));
            }
        }
        return result;
    }

    public static ExpandoObject ParseObject(JsonTextReader reader, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var obj = new ExpandoObject();
        while (reader.Read() && reader.TokenType != JsonToken.EndObject)
        {
            if (reader.TokenType != JsonToken.PropertyName) continue;
            
            var propertyName = reader.Value?.ToString();
            
            if (propertyName == null)
                throw new InvalidOperationException("Property name cannot be null.");
            
            reader.Read();
            switch (reader.TokenType)
            {
                case JsonToken.StartObject:
                    obj.TryAdd(propertyName, ParseObject(reader, cancellationToken));
                    break;
                case JsonToken.StartArray:
                    obj.TryAdd(propertyName, ParseInnerArray(reader, cancellationToken));
                    break;
                case JsonToken.Integer:
                case JsonToken.Float:
                case JsonToken.Boolean:
                    obj.TryAdd(propertyName, reader.Value);
                    break;
                case JsonToken.Null:
                    obj.TryAdd(propertyName, null);
                    break;
                case JsonToken.Undefined:
                case JsonToken.None:
                    obj.TryAdd(propertyName, null);
                    break;
                case JsonToken.StartConstructor:
                case JsonToken.PropertyName:
                case JsonToken.Comment:
                case JsonToken.Raw:
                case JsonToken.String:
                case JsonToken.EndObject:
                case JsonToken.EndArray:
                case JsonToken.EndConstructor:
                case JsonToken.Date:
                case JsonToken.Bytes:
                default:
                    obj.TryAdd(propertyName, reader.Value?.ToString());
                    break;
            }
        }
        return obj;
    }

    private static List<object> ParseInnerArray(JsonTextReader reader, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var result = new List<object>();
        while (reader.Read() && reader.TokenType != JsonToken.EndArray)
        {
            switch (reader.TokenType)
            {
                case JsonToken.StartObject:
                    result.Add(ParseObject(reader, cancellationToken));
                    break;
                case JsonToken.StartArray:
                    result.Add(ParseInnerArray(reader, cancellationToken));
                    break;
                default:
                    result.Add(reader.Value!);
                    break;
            }
        }
        return result;
    }
}