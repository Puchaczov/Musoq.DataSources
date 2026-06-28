using System;
using System.Collections.Generic;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Managers;
using Musoq.Schema.Optimization;
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
    private const string Interval = "interval";

    public TimeSchema() : base("time", CreateLibrary())
    {
    }

    public override ISchemaTable GetTableByName(
        string name,
        SourceMetadataContext metadataContext,
        params object[] parameters)
    {
        return name.ToLowerInvariant() switch
        {
            Interval => new TimeTable(),
            _ => throw new NotSupportedException($"Table {name} not found.")
        };
    }

    public override RowSource<T> GetRowSource<T>(
        string name,
        SourceExecutionContext executionContext,
        params object[] parameters)
    {
        return name.ToLowerInvariant() switch
        {
            Interval => EnsureSourceType<T, TimeEntity>(name, new TimeSource(
                DateTimeOffset.Parse((string)parameters[0]),
                DateTimeOffset.Parse((string)parameters[1]),
                (string)parameters[2],
                executionContext)),
            _ => throw new NotSupportedException($"Table {name} not found.")
        };
    }

    public override SourceDescriptor DescribeSource(
        string name,
        SourceDescribeContext context,
        params object[] parameters)
    {
        var table = GetTableByName(name, context.MetadataContext, parameters);

        return new SourceDescriptor
        {
            Identity = context.Identity,
            Columns = table.Columns,
            RowType = table.Metadata.TableEntityType,
            Diagnostics = [],
            ContractDiagnostics = []
        };
    }

    public override IReadOnlyList<SourceRuntimeSettingRequirement> DescribeSourceRuntimeSettings(
        string name,
        SourceRuntimeSettingsDescribeContext context,
        params object[] parameters)
    {
        return [];
    }

    public override SourcePlanResult TryPlanSource(string name, SourcePlanRequest request, params object[] parameters)
    {
        return TimeSourcePlanner.Plan(name, request);
    }

    public override SchemaMethodInfo[] GetConstructors()
    {
        return [CreateIntervalMethodInfo()];
    }

    public override SchemaMethodInfo[] GetRawConstructors(
        string methodName,
        SourceMetadataContext metadataContext)
    {
        return methodName.ToLowerInvariant() switch
        {
            Interval => [CreateIntervalMethodInfo()],
            _ => throw new NotSupportedException(
                $"Data source '{methodName}' is not supported by time schema. Available data sources: {Interval}")
        };
    }

    public override SchemaMethodInfo[] GetRawConstructors(SourceMetadataContext metadataContext)
    {
        return [CreateIntervalMethodInfo()];
    }

    private static SchemaMethodInfo CreateIntervalMethodInfo()
    {
        var constructorInfo = new ConstructorInfo(
            null!,
            false,
            [
                ("startDateTime", typeof(string)),
                ("stopDateTime", typeof(string)),
                ("interval", typeof(string))
            ]);

        return new SchemaMethodInfo(Interval, constructorInfo);
    }

    private static MethodsAggregator CreateLibrary()
    {
        var methodsManager = new MethodsManager();
        var library = new TimeLibrary();

        methodsManager.RegisterLibraries(library);

        return new MethodsAggregator(methodsManager);
    }
}
