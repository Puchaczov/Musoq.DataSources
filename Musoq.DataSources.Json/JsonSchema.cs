using Musoq.Schema.DataSources;
using Musoq.Schema.Helpers;
using Musoq.Schema.Managers;
using Musoq.Schema.Reflection;
using System.Collections.Generic;
using Musoq.Schema;

namespace Musoq.DataSources.Json
{
    /// <description>
    /// Provides schema to work with json files
    /// </description>
    /// <short-description>
    /// Provides schema to work with json files
    /// </short-description>
    /// <project-url>https://github.com/Puchaczov/Musoq.DataSources</project-url>
    public class JsonSchema : SchemaBase
    {
        private const string FileTable = "file";
        private const string SchemaName = "json";

        /// <virtual-constructors>
        /// <virtual-constructor>
        /// <virtual-param>Path to the json file</virtual-param>
        /// <virtual-param>Path to the json schema file</virtual-param>
        /// <examples>
        /// <example>
        /// <from>#json.file(string jsonFilePath, string jsonSchemaFilePath)</from>
        /// <description>Gives the ability to process json files</description>
        /// <columns isDynamic="true"></columns>
        /// </example>
        /// </examples>
        /// </virtual-constructor>
        /// </virtual-constructors>
        public JsonSchema()
            : base(SchemaName, CreateLibrary())
        {
        }

        /// <summary>
        /// Gets the table name based on the given data source and parameters
        /// </summary>
        /// <param name="name">Data Source name</param>
        /// <param name="runtimeContext">Runtime context</param>
        /// <param name="parameters">Parameters to pass to data source</param>
        /// <returns>Requested table metadata</returns>
        public override ISchemaTable GetTableByName(string name, RuntimeContext runtimeContext, params object[] parameters)
        {
            return new JsonBasedTable((string)parameters[1]);
        }

        /// <summary>
        /// Gets the data source based on the given data source and parameters.
        /// </summary>
        /// <param name="name">Data source name</param>
        /// <param name="interCommunicator">Runtime context</param>
        /// <param name="parameters">Parameters to pass data to data source</param>
        /// <returns>Data source</returns>
        public override RowSource GetRowSource(string name, RuntimeContext interCommunicator, params object[] parameters)
        {
            return new JsonSource((string)parameters[0], interCommunicator);
        }

        /// <summary>
        /// Gets information's about all tables in the schema.
        /// </summary>
        /// <returns>Data sources constructors</returns>
        public override SchemaMethodInfo[] GetConstructors()
        {
            var constructors = new List<SchemaMethodInfo>();

            constructors.AddRange(TypeHelper.GetSchemaMethodInfosForType<JsonSource>(FileTable));

            return constructors.ToArray();
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
    }
}