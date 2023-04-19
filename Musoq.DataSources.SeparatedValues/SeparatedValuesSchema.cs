using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Managers;

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
        /// <from>#separatedvalues.csv(string path, bool hasHeader, int skipLines)</from>
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
        /// <from>#separatedvalues.tsv(string path, bool hasHeader, int skipLines)</from>
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
            AddSource<SeparatedValuesSource>("csv");
            AddSource<SeparatedValuesSource>("tsv");
            AddSource<SeparatedValuesSource>("semicolon");
            AddTable<SeparatedValuesTable>("csv");
            AddTable<SeparatedValuesTable>("tsv");
            AddTable<SeparatedValuesTable>("semicolon");
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
                case "csv":
                    if (parameters[0] is IReadOnlyTable csvTable)
                        return new SeparatedValuesSource(csvTable, ",", interCommunicator);

                    return new SeparatedValuesSource((string)parameters[0], ",", (bool)parameters[1], (int)parameters[2], interCommunicator);
                case "tsv":
                    if (parameters[0] is IReadOnlyTable tsvTable)
                        return new SeparatedValuesSource(tsvTable, "\t", interCommunicator);

                    return new SeparatedValuesSource((string)parameters[0], "\t", (bool)parameters[1], (int)parameters[2], interCommunicator);
                case "semicolon":
                    if (parameters[0] is IReadOnlyTable semicolonTable)
                        return new SeparatedValuesSource(semicolonTable, ";", interCommunicator);

                    return new SeparatedValuesSource((string)parameters[0], ";", (bool)parameters[1], (int)parameters[2], interCommunicator);
            }

            return base.GetRowSource(name, interCommunicator, parameters);
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
                case "csv":
                    return new SeparatedValuesTable((string)parameters[0], ",", (bool)parameters[1], (int)parameters[2]);
                case "tsv":
                    return new SeparatedValuesTable((string)parameters[0], "\t", (bool)parameters[1], (int)parameters[2]);
                case "semicolon":
                    return new SeparatedValuesTable((string)parameters[0], ";", (bool)parameters[1], (int)parameters[2]);
            }

            return base.GetTableByName(name, parameters);
        }

        private static MethodsAggregator CreateLibrary()
        {
            var methodsManager = new MethodsManager();
            var propertiesManager = new PropertiesManager();

            var library = new SeparatedValuesLibrary();

            methodsManager.RegisterLibraries(library);
            propertiesManager.RegisterProperties(library);

            return new MethodsAggregator(methodsManager, propertiesManager);
        }
    }
}