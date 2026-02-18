using System;
using System.Collections.Generic;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Helpers;
using Musoq.Schema.Managers;
using Musoq.Schema.Reflection;

namespace Musoq.DataSources.Time;

/// <description>
///     Provides schema to work with time.
/// </description>
/// <short-description>
///     Provides schema to work with time.
/// </short-description>
/// <project-url>https://github.com/Puchaczov/Musoq.DataSources</project-url>
public class TimeSchema : SchemaBase
{
    /// <virtual-constructors>
    ///     <virtual-constructor>
    ///         <virtual-param>Start datetime</virtual-param>
    ///         <virtual-param>Stop datetime</virtual-param>
    ///         <virtual-param>interval</virtual-param>
    ///         <examples>
    ///             <example>
    ///                 <from>#time.interval(string startDateTime, string stopDateTime, string interval)</from>
    ///                 <description>Compute dates between two ranges</description>
    ///                 <columns>
    ///                     <column name="DateTime" type="DateTime">Gets the DateTime object</column>
    ///                     <column name="Second" type="int">Gets second of current computed DateTime</column>
    ///                     <column name="Minute" type="int">Gets minute of current computed DateTime</column>
    ///                     <column name="Hour" type="int">Gets the hour of current computed DateTime</column>
    ///                     <column name="Day" type="int">Gets the day of current computed DateTime</column>
    ///                     <column name="Month" type="int">Gets the month of current computed DateTime</column>
    ///                     <column name="Year" type="int">Gets the year of current computed DateTime</column>
    ///                     <column name="DayOfWeek" type="int">Gets the day of week of current computed DateTime</column>
    ///                     <column name="DayOfYear" type="int">Gets the day of year of current computed DateTime</column>
    ///                     <column name="TimeOfDay" type="TimeSpan">Gets the time of day of current computed DateTime</column>
    ///                 </columns>
    ///             </example>
    ///         </examples>
    ///     </virtual-constructor>
    /// </virtual-constructors>
    public TimeSchema() : base("time", CreateLibrary())
    {
    }

    /// <summary>
    ///     Gets the table name based on the given data source and parameters.
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
    ///     Gets the data source based on the given data source and parameters.
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
    ///     Gets information's about all tables in the schema.
    /// </summary>
    /// <returns>Data sources constructors</returns>
    public override SchemaMethodInfo[] GetConstructors()
    {
        var constructors = new List<SchemaMethodInfo>();

        constructors.AddRange(TypeHelper.GetSchemaMethodInfosForType<TimeSource>("interval"));

        return constructors.ToArray();
    }

    /// <summary>
    ///     Gets the constructor information for a specific data source method.
    /// </summary>
    /// <param name="methodName">The name of the method to get constructor information for.</param>
    /// <param name="runtimeContext">The runtime context.</param>
    /// <returns>An array of SchemaMethodInfo objects representing the method's constructors.</returns>
    public override SchemaMethodInfo[] GetRawConstructors(string methodName, RuntimeContext runtimeContext)
    {
        return methodName.ToLowerInvariant() switch
        {
            "interval" => [CreateIntervalMethodInfo()],
            _ => throw new NotSupportedException(
                $"Data source '{methodName}' is not supported by time schema. " +
                $"Available data sources: interval")
        };
    }

    /// <summary>
    ///     Gets constructor information for all data source methods.
    /// </summary>
    /// <param name="runtimeContext">The runtime context.</param>
    /// <returns>An array of all SchemaMethodInfo objects.</returns>
    public override SchemaMethodInfo[] GetRawConstructors(RuntimeContext runtimeContext)
    {
        return [CreateIntervalMethodInfo()];
    }

    private static SchemaMethodInfo CreateIntervalMethodInfo()
    {
        return TypeHelper.GetSchemaMethodInfosForType<TimeSource>("interval")[0];
    }

    private static MethodsAggregator CreateLibrary()
    {
        var methodsManager = new MethodsManager();
        var library = new TimeLibrary();

        methodsManager.RegisterLibraries(library);

        return new MethodsAggregator(methodsManager);
    }
}