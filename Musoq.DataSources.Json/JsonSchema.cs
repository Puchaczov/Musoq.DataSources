﻿using Musoq.Schema.DataSources;
using Musoq.Schema.Helpers;
using Musoq.Schema.Managers;
using Musoq.Schema.Reflection;
using System.Collections.Generic;
using Musoq.Schema;

namespace Musoq.DataSources.Json
{
    public class JsonSchema : SchemaBase
    {
        private const string FileTable = "file";
        private const string SchemaName = "json";

        public JsonSchema()
            : base(SchemaName, CreateLibrary())
        {
        }

        public override ISchemaTable GetTableByName(string name, params object[] parameters)
        {
            return new JsonBasedTable((string)parameters[1], (string)parameters[2]);
        }

        public override RowSource GetRowSource(string name, RuntimeContext communicator, params object[] parameters)
        {
            return new JsonSource((string)parameters[0], communicator);
        }

        private static MethodsAggregator CreateLibrary()
        {
            var methodsManager = new MethodsManager();
            var propertiesManager = new PropertiesManager();

            var library = new JsonLibrary();

            methodsManager.RegisterLibraries(library);
            propertiesManager.RegisterProperties(library);

            return new MethodsAggregator(methodsManager, propertiesManager);
        }

        public override SchemaMethodInfo[] GetConstructors()
        {
            var constructors = new List<SchemaMethodInfo>();

            constructors.AddRange(TypeHelper.GetSchemaMethodInfosForType<JsonSource>(FileTable));

            return constructors.ToArray();
        }
    }
}