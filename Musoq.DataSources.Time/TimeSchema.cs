using System;
using System.Collections.Generic;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Helpers;
using Musoq.Schema.Managers;
using Musoq.Schema.Reflection;

namespace Musoq.DataSources.Time
{
    /// <summary>
    /// Provides schema to work with time
    /// </summary>
    /// <short-description>
    /// Provides schema to work with time
    /// </short-description>
    /// <project-url>https://github.com/Puchaczov/Musoq.DataSources</project-url>
    public class TimeSchema : SchemaBase
    {
        /// <virtual-constructors>
        /// <virtual-constructor>
        /// <virtual-param>Start datetime</virtual-param>
        /// <virtual-param>Stop datetime</virtual-param>
        /// <virtual-param>interval</virtual-param>
        /// <examples>
        /// <example>
        /// <from>#time.interval(string startDateTime, string stopDateTime, string interval)</from>
        /// <description>Gets the zip files</description>
        /// <columns>
        /// <column name="DateTime" type="DateTime">Gets the file name of the entry in the zip archive</column>
        /// <column name="Second" type="int">Gets the file name of the entry in the zip archive</column>
        /// <column name="Minute" type="int">Gets the relative path of the entry in the zip archive</column>
        /// <column name="Hour" type="int">Gets the compressed size of the entry in the zip archive</column>
        /// <column name="Day" type="int">Gets the last time the entry in the zip archive was changed</column>
        /// <column name="Month" type="int">Gets the uncompressed size of the entry in the zip archive</column>
        /// <column name="Year" type="int">Determine whether the entry is a directory</column>
        /// <column name="DayOfWeek" type="int">Gets the nesting level</column>
        /// <column name="DayOfYear" type="int">Gets the nesting level</column>
        /// <column name="TimeOfDay" type="TimeSpan">Gets the nesting level</column>
        /// </columns>
        /// </example>
        /// </examples>
        /// </virtual-constructor>
        /// </virtual-constructors>
        public TimeSchema() : base("time", CreateLibrary())
        {
        }

        /// <summary>
        /// Gets the table name based on the given data source and parameters.
        /// </summary>
        /// <param name="name">Data Source name</param>
        /// <param name="runtimeContext">Runtime context</param>
        /// <param name="parameters">Parameters to pass to data source</param>
        /// <returns>Requested table metadata</returns>
        public override ISchemaTable GetTableByName(string name, RuntimeContext runtimeContext, params object[] parameters)
        {
            switch (name.ToLowerInvariant())
            {
                case "interval":
                    return new TimeTable();
            }

            throw new NotSupportedException($"Table {name} not found.");
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
                case "interval":
                    return new TimeSource(
                        DateTimeOffset.Parse((string)parameters[0]), 
                        DateTimeOffset.Parse((string)parameters[1]),
                        (string)parameters[2], 
                        interCommunicator);
            }

            throw new NotSupportedException($"Table {name} not found.");
        }
        
        /// <summary>
        /// Gets information's about all tables in the schema.
        /// </summary>
        /// <returns>Data sources constructors</returns>
        public override SchemaMethodInfo[] GetConstructors()
        {
            var constructors = new List<SchemaMethodInfo>();

            constructors.AddRange(TypeHelper.GetSchemaMethodInfosForType<TimeSource>("interval"));

            return constructors.ToArray();
        }
        
        private static MethodsAggregator CreateLibrary()
        {
            var methodsManager = new MethodsManager();
            var propertiesManager = new PropertiesManager();

            var library = new TimeLibrary();

            methodsManager.RegisterLibraries(library);
            propertiesManager.RegisterProperties(library);

            return new MethodsAggregator(methodsManager, propertiesManager);
        }
    }
}