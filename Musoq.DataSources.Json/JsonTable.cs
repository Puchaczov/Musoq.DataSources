using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Musoq.DataSources.Json
{
    internal class JsonTable : ISchemaTable
    {
        private readonly string _filePath;
        private ISchemaColumn[] _columns;

        public JsonTable(string filePath)
        {
            _filePath = filePath;
            _columns = null;
        }

        public ISchemaColumn[] Columns
        {
            get
            {
                if (_columns != null)
                    return _columns;
                
                using var contentStream = File.OpenRead(_filePath);
                using var contentReader = new StreamReader(contentStream);
                var jsonSchema = contentReader.ReadToEnd();
                var schema = JsonConvert.DeserializeObject(jsonSchema);

                _columns = schema switch
                {
                    JObject jObj => ParseObject(jObj),
                    JArray jArr => ParseArray(jArr),
                    _ => throw new NotSupportedException($"Unsupported object in schema {schema.GetType().Name}")
                };

                return _columns;
            }
        }
    
        public SchemaTableMetadata Metadata { get; } = new(typeof(object));
        
        public ISchemaColumn GetColumnByName(string name)
        {
            return Columns.SingleOrDefault(column => column.ColumnName == name);
        }

        public ISchemaColumn[] GetColumnsByName(string name)
        {
            return Columns.Where(column => column.ColumnName == name).ToArray();
        }

        private ISchemaColumn[] ParseArray(JArray jarr)
        {
            return new ISchemaColumn[]
            {
                new SchemaColumn("Array", 0, typeof(List<object>))
            };
        }

        private ISchemaColumn[] ParseObject(JObject jobj)
        {
            var obj = jobj;

            var props = new Stack<JProperty>();

            foreach (var prop in obj.Properties().Reverse())
                props.Push(prop);
            
            var columns = new List<ISchemaColumn>();
            var columnIndex = 0;

            while (props.Count > 0)
            {
                var prop = props.Pop();

                switch (prop.Value.Type)
                {
                    case JTokenType.None:
                        break;
                    case JTokenType.Object:
                        columns.Add(new SchemaColumn(prop.Name, columnIndex++, typeof(object)));
                        foreach (var mProp in ((JObject) prop.Value).Properties().Reverse())
                            props.Push(mProp);
                        break;
                    case JTokenType.Array:
                        columns.Add(new SchemaColumn(prop.Name, columnIndex++, typeof(List<object>)));
                        break;
                    case JTokenType.Constructor:
                        break;
                    case JTokenType.Property:
                        break;
                    case JTokenType.Comment:
                        break;
                    case JTokenType.Integer:
                        columns.Add(new SchemaColumn(prop.Name, columnIndex++, GetType(prop.Value)));
                        break;
                    case JTokenType.Float:
                        columns.Add(new SchemaColumn(prop.Name, columnIndex++, GetType(prop.Value)));
                        break;
                    case JTokenType.String:
                        columns.Add(new SchemaColumn(prop.Name, columnIndex++, GetType(prop.Value)));
                        break;
                    case JTokenType.Boolean:
                        columns.Add(new SchemaColumn(prop.Name, columnIndex++, GetType(prop.Value)));
                        break;
                    case JTokenType.Null:
                        columns.Add(new SchemaColumn(prop.Name, columnIndex++, typeof(object)));
                        break;
                    case JTokenType.Undefined:
                    case JTokenType.Date:
                    case JTokenType.Raw:
                    case JTokenType.Bytes:
                    case JTokenType.Guid:
                    case JTokenType.Uri:
                    case JTokenType.TimeSpan:
                        columns.Add(new SchemaColumn(prop.Name, columnIndex++, typeof(string)));
                        break;
                }
            }

            return columns.ToArray();
        }

        private static Type GetType(JToken value)
        {
            switch (value.Value<string>()?.ToLowerInvariant())
            {
                case "float":
                    return typeof(decimal);
                case "int":
                case "long":
                    return typeof(long);
                case "string":
                    return typeof(string);
                case "bool":
                case "boolean":
                    return typeof(bool);
            }

            throw new NotSupportedException($"Type {value.Value<string>()} is not supported.");
        }
    }
}