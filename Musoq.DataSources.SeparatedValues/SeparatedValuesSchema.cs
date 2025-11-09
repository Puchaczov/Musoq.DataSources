using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Helpers;
using Musoq.Schema.Managers;
using Musoq.Schema.Reflection;

namespace Musoq.DataSources.SeparatedValues
{
    /// <description>
    /// Provides schema to work with separated values like .csv, .tsv, semicolon.
    /// </description>
    /// <short-description>
    /// Provides schema to work with separated values like .csv, .tsv, semicolon.
    /// </short-description>
    /// <project-url>https://github.com/Puchaczov/Musoq.DataSources</project-url>
    public class SeparatedValuesSchema : SchemaBase
    {
        private const string SchemaName = "SeparatedValues";

        /// <virtual-constructors>
        /// <virtual-constructor>
        /// <virtual-param>Path to the given file</virtual-param>
        /// <virtual-param>Does the file has header</virtual-param>
        /// <virtual-param>How many lines should be skipped</virtual-param>
        /// <examples>
        /// <example>
        /// <from>#separatedvalues.comma(string path, bool hasHeader, int skipLines)</from>
        /// <description>Gives the ability to process .CSV files</description>
        /// <columns isDynamic="true"></columns>
        /// </example>
        /// </examples>
        /// </virtual-constructor>
        /// <virtual-constructor>
        /// <virtual-param>Path to the given file</virtual-param>
        /// <virtual-param>Does the file has header</virtual-param>
        /// <virtual-param>How many lines should be skipped</virtual-param>
        /// <examples>
        /// <example>
        /// <from>#separatedvalues.tab(string path, bool hasHeader, int skipLines)</from>
        /// <description>Gives the ability to process .TSV files</description>
        /// <columns isDynamic="true"></columns>
        /// </example>
        /// </examples>
        /// </virtual-constructor>
        /// <virtual-constructor>
        /// <virtual-param>Path to the given file</virtual-param>
        /// <virtual-param>Does the file has header</virtual-param>
        /// <virtual-param>How many lines should be skipped</virtual-param>
        /// <examples>
        /// <example>
        /// <from>#separatedvalues.semicolon(string path, bool hasHeader, int skipLines)</from>
        /// <description>Gives the ability to process semicolon files</description>
        /// <columns isDynamic="true"></columns>
        /// </example>
        /// </examples>
        /// </virtual-constructor>
        /// </virtual-constructors>
        public SeparatedValuesSchema()
            : base(SchemaName.ToLowerInvariant(), CreateLibrary())
        {
            AddSource<SeparatedValuesFromFileRowsSource>("comma");
            AddSource<SeparatedValuesFromFileRowsSource>("tab");
            AddSource<SeparatedValuesFromFileRowsSource>("semicolon");
            AddTable<SeparatedValuesTable>("comma");
            AddTable<SeparatedValuesTable>("tab");
            AddTable<SeparatedValuesTable>("semicolon");
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
                case "comma":
                    if (runtimeContext.QueryInformation.HasExternallyProvidedTypes)
                        return new InitiallyInferredTable(runtimeContext.AllColumns);
                    
                    return new SeparatedValuesTable((string)parameters[0], ",", (bool)parameters[1], (int)parameters[2]) { InferredColumns = runtimeContext.AllColumns };
                case "tab":
                    if (runtimeContext.QueryInformation.HasExternallyProvidedTypes)
                        return new InitiallyInferredTable(runtimeContext.AllColumns);
                    
                    return new SeparatedValuesTable((string)parameters[0], "\t", (bool)parameters[1], (int)parameters[2]) { InferredColumns = runtimeContext.AllColumns };
                case "semicolon":
                    if (runtimeContext.QueryInformation.HasExternallyProvidedTypes)
                        return new InitiallyInferredTable(runtimeContext.AllColumns);
                    
                    return new SeparatedValuesTable((string)parameters[0], ";", (bool)parameters[1], (int)parameters[2]) { InferredColumns = runtimeContext.AllColumns };
            }

            return base.GetTableByName(name, runtimeContext, parameters);
        }

        /// <summary>
        /// Gets the data source based on the given data source and parameters.
        /// </summary>
        /// <param name="name">Data source name</param>
        /// <param name="runtimeContext">Runtime context</param>
        /// <param name="parameters">Parameters to pass data to data source</param>
        /// <returns>Data source</returns>
        public override RowSource GetRowSource(string name, RuntimeContext runtimeContext, params object[] parameters)
        {
            switch (name.ToLowerInvariant())
            {
                case "comma":
                    if (parameters[0] is IReadOnlyTable csvTable)
                        return new SeparatedValuesFromFileRowsSource(csvTable, ",", runtimeContext.EndWorkToken) { RuntimeContext = runtimeContext };
                    
                    if (parameters[0] is Stream csvStream)
                        return new SeparatedValuesFromStreamRowsSource(csvStream, ",", (bool)parameters[1], (int)parameters[2], runtimeContext);

                    return new SeparatedValuesFromFileRowsSource((string)parameters[0], ",", (bool)parameters[1], (int)parameters[2], runtimeContext.EndWorkToken) { RuntimeContext = runtimeContext };
                case "tab":
                    if (parameters[0] is IReadOnlyTable tsvTable)
                        return new SeparatedValuesFromFileRowsSource(tsvTable, "\t", runtimeContext.EndWorkToken) { RuntimeContext = runtimeContext };
                    
                    if (parameters[0] is Stream tsvStream)
                        return new SeparatedValuesFromStreamRowsSource(tsvStream, "\t", (bool)parameters[1], (int)parameters[2], runtimeContext);

                    return new SeparatedValuesFromFileRowsSource((string)parameters[0], "\t", (bool)parameters[1], (int)parameters[2], runtimeContext.EndWorkToken) { RuntimeContext = runtimeContext };
                case "semicolon":
                    if (parameters[0] is IReadOnlyTable semicolonTable)
                        return new SeparatedValuesFromFileRowsSource(semicolonTable, ";", runtimeContext.EndWorkToken) { RuntimeContext = runtimeContext };
                    
                    if (parameters[0] is Stream semicolonStream)
                        return new SeparatedValuesFromStreamRowsSource(semicolonStream, ";", (bool)parameters[1], (int)parameters[2], runtimeContext);

                    return new SeparatedValuesFromFileRowsSource((string)parameters[0], ";", (bool)parameters[1], (int)parameters[2], runtimeContext.EndWorkToken) { RuntimeContext = runtimeContext };
            }

            return base.GetRowSource(name, runtimeContext, parameters);
        }

        /// <summary>
        /// Gets the constructor information for a specific data source method.
        /// </summary>
        /// <param name="methodName">The name of the method to get constructor information for.</param>
        /// <param name="runtimeContext">The runtime context.</param>
        /// <returns>An array of SchemaMethodInfo objects representing the method's constructors.</returns>
        public override SchemaMethodInfo[] GetRawConstructors(string methodName, RuntimeContext runtimeContext)
        {
            return methodName.ToLowerInvariant() switch
            {
                "comma" => [CreateCommaMethodInfo()],
                "tab" => [CreateTabMethodInfo()],
                "semicolon" => [CreateSemicolonMethodInfo()],
                _ => throw new NotSupportedException(
                    $"Data source '{methodName}' is not supported by {SchemaName} schema. " +
                    $"Available data sources: comma, tab, semicolon")
            };
        }

        /// <summary>
        /// Gets constructor information for all data source methods.
        /// </summary>
        /// <param name="runtimeContext">The runtime context.</param>
        /// <returns>An array of all SchemaMethodInfo objects.</returns>
        public override SchemaMethodInfo[] GetRawConstructors(RuntimeContext runtimeContext)
        {
            return
            [
                CreateCommaMethodInfo(),
                CreateTabMethodInfo(),
                CreateSemicolonMethodInfo()
            ];
        }

        private static SchemaMethodInfo CreateCommaMethodInfo()
        {
            var constructorInfo = new ConstructorInfo(
                originConstructorInfo: null!,
                supportsInterCommunicator: false,
                arguments:
                [
                    ("path", typeof(string)),
                    ("hasHeader", typeof(bool)),
                    ("skipLines", typeof(int))
                ]
            );

            return new SchemaMethodInfo("comma", constructorInfo);
        }

        private static SchemaMethodInfo CreateTabMethodInfo()
        {
            var constructorInfo = new ConstructorInfo(
                originConstructorInfo: null!,
                supportsInterCommunicator: false,
                arguments:
                [
                    ("path", typeof(string)),
                    ("hasHeader", typeof(bool)),
                    ("skipLines", typeof(int))
                ]
            );

            return new SchemaMethodInfo("tab", constructorInfo);
        }

        private static SchemaMethodInfo CreateSemicolonMethodInfo()
        {
            var constructorInfo = new ConstructorInfo(
                originConstructorInfo: null!,
                supportsInterCommunicator: false,
                arguments:
                [
                    ("path", typeof(string)),
                    ("hasHeader", typeof(bool)),
                    ("skipLines", typeof(int))
                ]
            );

            return new SchemaMethodInfo("semicolon", constructorInfo);
        }

        private static MethodsAggregator CreateLibrary()
        {
            var methodsManager = new MethodsManager();
            var library = new SeparatedValuesLibrary();

            methodsManager.RegisterLibraries(library);

            return new MethodsAggregator(methodsManager);
        }
    }
}