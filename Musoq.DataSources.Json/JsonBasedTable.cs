using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Musoq.DataSources.Json
{
    /// <summary>
    /// Represents a json based table.
    /// </summary>
    public class JsonBasedTable : ISchemaTable
    {
        private readonly string _path;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonBasedTable"/> class.
        /// </summary>
        /// <param name="filePath"></param>
        public JsonBasedTable(string filePath)
        {
            _path = filePath;
        }

        /// <summary>
        /// Gets columns from json file.
        /// </summary>
        /// <exception cref="NotSupportedException"></exception>
        public ISchemaColumn[] Columns
        {
            get
            {
                var file = File.ReadAllText(_path);
                var schema = JsonConvert.DeserializeObject(file);

                switch (schema)
                {
                    case JObject jobj:
                        return ParseObject(jobj);
                    case JArray jarr:
                        return ParseArray(jarr);
                }

                throw new NotSupportedException($"Unsupported object in schema {schema.GetType().Name}");
            }
        }

        /// <summary>
        /// Gets the column by name.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public ISchemaColumn GetColumnByName(string name)
        {
            return Columns.SingleOrDefault(column => column.ColumnName == name);
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
                        columns.Add(new SchemaColumn(prop.Name, columnIndex++, typeof(ExpandoObject)));
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