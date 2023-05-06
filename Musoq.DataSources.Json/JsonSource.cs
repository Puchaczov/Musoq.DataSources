using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading;
using Musoq.Schema.DataSources;
using Newtonsoft.Json;

namespace Musoq.DataSources.Json
{
    /// <summary>
    /// Represents a json source.
    /// </summary>
    public class JsonSource : RowSourceBase<dynamic>
    {
        private readonly Stream _stream;
        private readonly CancellationToken _endWorkToken;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="JsonSource"/> class.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="endWorkToken"></param>
        public JsonSource(Stream stream, CancellationToken endWorkToken)
        {
            _stream = stream;
            _endWorkToken = endWorkToken;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonSource"/> class.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="endWorkToken"></param>
        public JsonSource(string path, CancellationToken endWorkToken)
        {
            _endWorkToken = endWorkToken;
            _stream = File.OpenRead(path);
        }

        /// <summary>
        /// Gets the data from json file.
        /// </summary>
        /// <param name="chunkedSource"></param>
        /// <exception cref="NotSupportedException"></exception>
        protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
        {
            using var contentStream = _stream;
            using var contentReader = new StreamReader(contentStream);
            using var reader = new JsonTextReader(contentReader);
            reader.SupportMultipleContent = true;

            if (!reader.Read())
                throw new NotSupportedException("Cannot read file. Json is probably malformed.");
            
            var serializer = new JsonSerializer();
            var rows = reader.TokenType switch
            {
                JsonToken.StartObject => new[] { ParseObject(serializer, reader) },
                JsonToken.StartArray => ParseArray(serializer, reader),
                _ => null
            };

            if (rows == null)
                throw new NotSupportedException("This type of .json file is not supported.");

            using var enumerator = rows.GetEnumerator();

            if (!enumerator.MoveNext())
                return;

            if (enumerator.Current is not IDictionary<string, object> firstRow)
                return;

            var index = 0;
            var indexToNameMap = firstRow.Keys.ToDictionary(_ => index++);
            
            var list = new List<IObjectResolver>
            {
                new JsonObjectResolver(firstRow, indexToNameMap)
            };
            
            while (enumerator.MoveNext())
            {
                _endWorkToken.ThrowIfCancellationRequested();
                
                if (enumerator.Current is not IDictionary<string, object> row)
                    continue;

                list.Add(new JsonObjectResolver(row, indexToNameMap));

                if (list.Count < 1000)
                    continue;

                chunkedSource.Add(list, _endWorkToken);

                list = new List<IObjectResolver>(1000);
            }
            
            chunkedSource.Add(list, _endWorkToken);
        }


        private IEnumerable<ExpandoObject> ParseArray(JsonSerializer serializer, JsonTextReader reader)
        {
            _endWorkToken.ThrowIfCancellationRequested();
            
            var result = new List<ExpandoObject>();
            while (reader.Read() && reader.TokenType != JsonToken.EndArray)
            {
                if (reader.TokenType == JsonToken.StartObject)
                {
                    result.Add(ParseObject(serializer, reader));
                }
            }
            return result;
        }

        private List<object> ParseInnerArray(JsonSerializer serializer, JsonTextReader reader)
        {
            _endWorkToken.ThrowIfCancellationRequested();
            
            var result = new List<object>();
            while (reader.Read() && reader.TokenType != JsonToken.EndArray)
            {
                switch (reader.TokenType)
                {
                    case JsonToken.StartObject:
                        result.Add(ParseObject(serializer, reader));
                        break;
                    case JsonToken.StartArray:
                        result.Add(ParseInnerArray(serializer, reader));
                        break;
                    default:
                        result.Add(reader.Value!);
                        break;
                }
            }
            return result;
        }

        private ExpandoObject ParseObject(JsonSerializer serializer, JsonTextReader reader)
        {
            _endWorkToken.ThrowIfCancellationRequested();
            
            var obj = new ExpandoObject();
            while (reader.Read() && reader.TokenType != JsonToken.EndObject)
            {
                if (reader.TokenType != JsonToken.PropertyName) continue;
                
                var propertyName = reader.Value!.ToString();
                reader.Read();
                switch (reader.TokenType)
                {
                    case JsonToken.StartObject:
                        obj.TryAdd(propertyName, ParseObject(serializer, reader));
                        break;
                    case JsonToken.StartArray:
                        obj.TryAdd(propertyName, ParseInnerArray(serializer, reader));
                        break;
                    case JsonToken.Integer:
                    case JsonToken.Float:
                    case JsonToken.Boolean:
                        obj.TryAdd(propertyName, reader.Value!);
                        break;
                    case JsonToken.None:
                    case JsonToken.StartConstructor:
                    case JsonToken.PropertyName:
                    case JsonToken.Comment:
                    case JsonToken.Raw:
                    case JsonToken.Null:
                    case JsonToken.String:
                    case JsonToken.Undefined:
                    case JsonToken.EndObject:
                    case JsonToken.EndArray:
                    case JsonToken.EndConstructor:
                    case JsonToken.Date:
                    case JsonToken.Bytes:
                    default:
                        obj.TryAdd(propertyName, reader.Value.ToString());
                        break;
                }
            }
            return obj;
        }
    }
}