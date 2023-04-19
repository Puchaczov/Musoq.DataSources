using Musoq.Schema.DataSources;
using Musoq.Schema.Exceptions;
using Musoq.Schema.Helpers;
using Musoq.Schema.Managers;
using Musoq.Schema.Reflection;
using System.Collections.Generic;

namespace Musoq.Schema.Xml
{

    /// <description>
    /// Provides schema to work with xml files
    /// </description>
    /// <short-description>
    /// Provides schema to work with xml files
    /// </short-description>
    /// <project-url>https://github.com/Puchaczov/Musoq.DataSources</project-url>
    public class XmlSchema : SchemaBase
    {

        /// <virtual-constructors>
        /// <virtual-constructor>
        /// <virtual-param>Path to xml file</virtual-param>
        /// <examples>
        /// <example>
        /// <from>#xml.file(string path)</from>
        /// <description>Gives the ability to process .xml files</description>
        /// <columns isDynamic="true"></columns>
        /// </example>
        /// </examples>
        /// </virtual-constructor>
        /// </virtual-constructors>
        public XmlSchema() 
            : base("Xml", CreateLibrary())
        {
        }
        
        /// <summary>
        /// Gets the table name based on the given data source and parameters.
        /// </summary>
        /// <param name="name">Data Source name</param>
        /// <param name="parameters">Parameters to pass to data source</param>
        /// <returns>Requested table metadata</returns>
        public override ISchemaTable GetTableByName(string name, params object[] parameters)
        {
            switch (name.ToLowerInvariant())
            {
                case "file":
                    return new XmlFileTable();
            }

            throw new TableNotFoundException(nameof(name));
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
            switch (name.ToLowerInvariant())
            {
                case "file":
                    return new XmlSource((string)parameters[0], interCommunicator);
            }

            throw new SourceNotFoundException(nameof(name));
        }

        /// <summary>
        /// Gets information's about all tables in the schema.
        /// </summary>
        /// <returns>Data sources constructors</returns>
        public override SchemaMethodInfo[] GetConstructors()
        {
            var constructors = new List<SchemaMethodInfo>();

            constructors.AddRange(TypeHelper.GetSchemaMethodInfosForType<XmlSource>("file"));

            return constructors.ToArray();
        }

        private static MethodsAggregator CreateLibrary()
        {
            var methodsManager = new MethodsManager();
            var propertiesManager = new PropertiesManager();

            var library = new XmlLibrary();

            methodsManager.RegisterLibraries(library);
            propertiesManager.RegisterProperties(library);

            return new MethodsAggregator(methodsManager, propertiesManager);
        }
    }
}
