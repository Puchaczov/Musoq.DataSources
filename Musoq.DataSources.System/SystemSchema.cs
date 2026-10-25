using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Managers;
using Musoq.Schema.Optimization;
using Musoq.Schema.Reflection;

namespace Musoq.DataSources.System;

/// <description>
///     System schema helper methods
/// </description>
/// <short-description>
///     System schema helper methods
/// </short-description>
/// <project-url>https://github.com/Puchaczov/Musoq.DataSources</project-url>
public class SystemSchema : SchemaBase
{
    private const string Dual = "dual";
    private const string Range = "range";
    private const string System = "system";

    public SystemSchema()
        : base(System, CreateLibrary())
    {
    }

    public override ISchemaTable GetTableByName(
        string name,
        SourceMetadataContext metadataContext,
        params object[] parameters)
    {
        return name.ToLowerInvariant() switch
        {
            Dual => new DualTable(),
            Range => new RangeTable(),
            _ => throw new NotSupportedException(name)
        };
    }

    public override RowSource<T> GetRowSource<T, TArguments>(
        string name,
        SourceExecutionContext executionContext,
        TArguments parameters)
    {
        return name.ToLowerInvariant() switch
        {
            Dual => EnsureSourceType<T, DualEntity>(name, new DualRowSource(executionContext)),
            Range when parameters.Count == 1 => EnsureSourceType<T, RangeItemEntity>(
                name,
                new RangeSource(0, ReadRangeArgument(parameters, 0), executionContext)),
            Range when parameters.Count == 2 => EnsureSourceType<T, RangeItemEntity>(
                name,
                new RangeSource(
                    ReadRangeArgument(parameters, 0),
                    ReadRangeArgument(parameters, 1),
                    executionContext)),
            _ => throw new NotSupportedException(name)
        };
    }

    private static long ReadRangeArgument<TArguments>(TArguments arguments, int index)
        where TArguments : struct, ISourceArguments
    {
        var reader = new RangeArgumentReader(index);
        arguments.Visit(ref reader);
        return reader.Value;
    }

    // Match the lossless integral widening accepted by Core's long overload.
    private struct RangeArgumentReader(int index) : IColumnValueVisitor
    {
        internal long Value;
        public void Visit<TValue>(int ordinal, in TValue value)
        {
            if (ordinal != index) return;
            ref var item = ref Unsafe.AsRef(in value);
            if (typeof(TValue) == typeof(long)) Value = Unsafe.As<TValue, long>(ref item);
            else if (typeof(TValue) == typeof(int)) Value = Unsafe.As<TValue, int>(ref item);
            else if (typeof(TValue) == typeof(uint)) Value = Unsafe.As<TValue, uint>(ref item);
            else if (typeof(TValue) == typeof(short)) Value = Unsafe.As<TValue, short>(ref item);
            else if (typeof(TValue) == typeof(ushort)) Value = Unsafe.As<TValue, ushort>(ref item);
            else if (typeof(TValue) == typeof(byte)) Value = Unsafe.As<TValue, byte>(ref item);
            else if (typeof(TValue) == typeof(sbyte)) Value = Unsafe.As<TValue, sbyte>(ref item);
            else if (typeof(TValue) == typeof(char)) Value = Unsafe.As<TValue, char>(ref item);
            else throw new InvalidOperationException("A range argument requires a lossless integral conversion to Int64.");
        }
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
        return SystemSourcePlanner.Plan(name, request);
    }

    public override SchemaMethodInfo[] GetConstructors()
    {
        var constructors = new List<SchemaMethodInfo>
        {
            CreateDualMethodInfo()
        };

        constructors.AddRange(CreateRangeMethodInfos());

        return constructors.ToArray();
    }

    public override SchemaMethodInfo[] GetRawConstructors(
        string methodName,
        SourceMetadataContext metadataContext)
    {
        return methodName.ToLowerInvariant() switch
        {
            Dual => [CreateDualMethodInfo()],
            Range => CreateRangeMethodInfos(),
            _ => throw new NotSupportedException(
                $"Data source '{methodName}' is not supported by {System} schema. " +
                $"Available data sources: {Dual}, {Range}")
        };
    }

    public override SchemaMethodInfo[] GetRawConstructors(SourceMetadataContext metadataContext)
    {
        return GetConstructors();
    }

    private static SchemaMethodInfo CreateDualMethodInfo()
    {
        return new SchemaMethodInfo(Dual, new ConstructorInfo(null!, false, []));
    }

    private static SchemaMethodInfo[] CreateRangeMethodInfos()
    {
        var rangeInfo1 = new ConstructorInfo(
            null!,
            false,
            [
                ("max", typeof(long))
            ]);

        var rangeInfo2 = new ConstructorInfo(
            null!,
            false,
            [
                ("min", typeof(long)),
                ("max", typeof(long))
            ]);

        return
        [
            new SchemaMethodInfo(Range, rangeInfo1),
            new SchemaMethodInfo(Range, rangeInfo2)
        ];
    }

    private static MethodsAggregator CreateLibrary()
    {
        var methodsManager = new MethodsManager();
        var library = new EmptyLibrary();

        methodsManager.RegisterLibraries(library);

        return new MethodsAggregator(methodsManager);
    }
}
