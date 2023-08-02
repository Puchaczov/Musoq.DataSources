using System;
using System.Collections.Generic;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Helpers;
using Musoq.Schema.Managers;
using Musoq.Schema.Reflection;

namespace Musoq.DataSources.System
{
    /// <description>
    /// System schema helper methods
    /// </description>
    /// <short-description>
    /// System schema helper methods
    /// </short-description>
    /// <project-url>https://github.com/Puchaczov/Musoq.DataSources</project-url>
    public class SystemSchema : SchemaBase
    {
        private const string Dual = "dual";
        private const string Range = "range";
        private const string System = "system";
        
        /// <virtual-constructors>
        /// <virtual-constructor>
        /// <examples>
        /// <example>
        /// <from>
        /// #system.dual()
        /// </from>
        /// <description>The dummy table</description>
        /// <columns>
        /// <column name="Dummy" type="string">Just empty string</column>
        /// </columns>
        /// </example>
        /// </examples>
        /// </virtual-constructor>
        /// <virtual-constructor>
        /// <virtual-param>Minimal value</virtual-param>
        /// <virtual-param>Maximal value</virtual-param>
        /// <examples>
        /// <example>
        /// <from>
        /// #system.range(long min, long max)
        /// </from>
        /// <description>Gives the ability to generate ranged values</description>
        /// <columns>
        /// <column name="Value" type="long">Enumerated value</column>
        /// </columns>
        /// </example>
        /// <example>
        /// <from>
        /// #system.range(int min, int max)
        /// </from>
        /// <description>Gives the ability to generate ranged values</description>
        /// <columns>
        /// <column name="Value" type="long">Enumerated value</column>
        /// </columns>
        /// </example>
        /// </examples>
        /// </virtual-constructor>
        /// <virtual-constructor>
        /// <virtual-param>Maximal value</virtual-param>
        /// <examples>
        /// <example>
        /// <from>#system.range(long max)</from>
        /// <description>Gives the ability to generate ranged values</description>
        /// <columns>
        /// <column name="Value" type="long">Enumerated value</column>
        /// </columns>
        /// </example>
        /// </examples>
        /// </virtual-constructor>
        /// </virtual-constructors>
        public SystemSchema() 
            : base(System, CreateLibrary())
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
                case Dual:
                    return new DualTable();
                case Range:
                    return new RangeTable();
            }

            throw new NotSupportedException(name);
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
                case Dual:
                    return new DualRowSource();
                case Range:
                    {
                        switch(parameters.Length)
                        {
                            case 1:
                                return new RangeSource(0, Convert.ToInt64(parameters[0]));
                            case 2:
                                return new RangeSource(Convert.ToInt64(parameters[0]), Convert.ToInt64(parameters[1]));
                        }
                        break;
                    }
            }

            throw new NotSupportedException(name);
        }

        /// <summary>
        /// Gets information's about all tables in the schema.
        /// </summary>
        /// <returns>Data sources constructors</returns>
        public override SchemaMethodInfo[] GetConstructors()
        {
            var constructors = new List<SchemaMethodInfo>();

            constructors.AddRange(TypeHelper.GetSchemaMethodInfosForType<DualRowSource>(Dual));
            constructors.AddRange(TypeHelper.GetSchemaMethodInfosForType<RangeSource>(Range));

            return constructors.ToArray();
        }

        private static MethodsAggregator CreateLibrary()
        {
            var methodsManager = new MethodsManager();
            var library = new EmptyLibrary();

            methodsManager.RegisterLibraries(library);

            return new MethodsAggregator(methodsManager);
        }
    }
}
