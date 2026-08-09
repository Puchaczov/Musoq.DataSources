using System.Dynamic;
using System.Text;
using Newtonsoft.Json;

namespace Musoq.DataSources.Json.Benchmarks;

internal static class FrozenLegacyJsonAdapter
{
    public static IReadOnlyList<object?[]> Read(string path, IReadOnlyList<string> columns)
    {
        using var stream = File.OpenRead(path);
        using var textReader = new StreamReader(stream, new UTF8Encoding(false, true), false, 1024 * 1024);
        using var reader = new JsonTextReader(textReader)
        {
            SupportMultipleContent = true
        };

        if (!reader.Read())
            throw new InvalidDataException("Cannot read legacy JSON fixture.");

        var objects = reader.TokenType switch
        {
            JsonToken.StartObject => new List<ExpandoObject> { ParseObject(reader) },
            JsonToken.StartArray => ParseArray(reader),
            _ => throw new NotSupportedException("Legacy JSON root is not supported.")
        };
        var result = new List<object?[]>(objects.Count);

        foreach (IDictionary<string, object?> item in objects)
        {
            var row = new object?[columns.Count];
            for (var index = 0; index < columns.Count; index++)
                item.TryGetValue(columns[index], out row[index]);
            result.Add(row);
        }

        return result;
    }

    private static List<ExpandoObject> ParseArray(JsonTextReader reader)
    {
        var result = new List<ExpandoObject>();
        while (reader.Read() && reader.TokenType != JsonToken.EndArray)
            if (reader.TokenType == JsonToken.StartObject)
                result.Add(ParseObject(reader));
        return result;
    }

    private static ExpandoObject ParseObject(JsonTextReader reader)
    {
        var result = new ExpandoObject();
        var dictionary = (IDictionary<string, object?>)result;

        while (reader.Read() && reader.TokenType != JsonToken.EndObject)
        {
            if (reader.TokenType != JsonToken.PropertyName)
                continue;

            var propertyName = reader.Value?.ToString()
                               ?? throw new InvalidDataException("Legacy JSON property name is null.");
            if (!reader.Read())
                throw new InvalidDataException("Legacy JSON property has no value.");

            var value = reader.TokenType switch
            {
                JsonToken.StartObject => ParseObject(reader),
                JsonToken.StartArray => ParseInnerArray(reader),
                JsonToken.Integer or JsonToken.Float or JsonToken.Boolean => reader.Value,
                JsonToken.Null or JsonToken.Undefined or JsonToken.None => null,
                _ => reader.Value?.ToString()
            };
            dictionary.TryAdd(propertyName, value);
        }

        return result;
    }

    private static List<object?> ParseInnerArray(JsonTextReader reader)
    {
        var result = new List<object?>();
        while (reader.Read() && reader.TokenType != JsonToken.EndArray)
        {
            result.Add(reader.TokenType switch
            {
                JsonToken.StartObject => ParseObject(reader),
                JsonToken.StartArray => ParseInnerArray(reader),
                _ => reader.Value
            });
        }

        return result;
    }
}
