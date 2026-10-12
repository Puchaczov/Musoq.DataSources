# Musoq Plugin Development Tutorial - Native typed execution

This standalone tutorial describes the accepted library-owned typed execution contracts after Core 11B, DataSources 12C and Cloud 13C. It replaces intermediate runtime-v2 migration examples that used removed object transfer boundaries. Use the intended host contract, not a historical package version. Target net10.0 with the repository-pinned SDK or a compatible newer 10.0 feature band.

## Complete example project

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Version>1.0.0</Version>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);CS1591</NoWarn>
    <PackageId>Musoq.DataSources.InvoiceExample</PackageId>
    <Authors>Musoq contributors</Authors>
    <Description>Executable native plugin authoring example.</Description>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://github.com/Puchaczov/Musoq.DataSources</PackageProjectUrl>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Musoq.Schema" Version="$(MusoqSchemaVersion)" ExcludeAssets="runtime" />
    <PackageReference Include="Musoq.Plugins" Version="$(MusoqPluginsVersion)" ExcludeAssets="runtime" />
  </ItemGroup>
</Project>
```

`AssemblyInfo.cs`:

```csharp
using Musoq.Schema.Attributes;
[assembly: PluginSchemas("invoice")]
```

`Plugin.cs` (the exact compiled example):

```csharp
using Musoq.Plugins;
using Musoq.Plugins.Attributes;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Managers;
using Musoq.Schema.Optimization;
using Musoq.Schema.Reflection;

namespace Musoq.DataSources.InvoiceExample;

public sealed record Invoice(int Position, string Customer, decimal? Amount, int? Quantity);

/// <description>Native invoice authoring example.</description>
/// <short-description>Typed invoice rows and custom functions.</short-description>
public sealed class InvoiceSchema : SchemaBase
{
    /// <virtual-constructors>
    /// <virtual-constructor>
    /// <examples><example>
    /// <from>invoice.rows(string customer)</from>
    /// <description>Use '*' for every customer.</description>
    /// <columns><column name="Position" type="int" />
    /// <column name="Customer" type="string" />
    /// <column name="Amount" type="decimal?" />
    /// <column name="Quantity" type="int?" /></columns>
    /// </example></examples>
    /// </virtual-constructor>
    /// </virtual-constructors>
    public InvoiceSchema() : base("invoice", CreateLibrary()) { }

    public override ISchemaTable GetTableByName(string name, SourceMetadataContext context,
        params object?[] parameters)
    {
        EnsureName(name);
        return new InvoiceTable();
    }

    public override RowSource<T> GetRowSource<T, TArguments>(string name,
        SourceExecutionContext context, TArguments arguments)
    {
        EnsureName(name);
        if (arguments.Count != 1)
            throw new ArgumentException("invoice.rows requires one string customer.", nameof(arguments));
        return EnsureSourceType<T, Invoice>(name, new InvoiceSource(arguments.GetValue<string>(0), context));
    }

    public override SourcePlanResult TryPlanSource(string name, SourcePlanRequest request,
        params object?[] parameters)
    {
        EnsureName(name);
        return SourcePlanResult.RejectAll(request);
    }

    public override SchemaMethodInfo[] GetRawConstructors(SourceMetadataContext context)
        => [new("rows", new ConstructorInfo(null!, false, [("customer", typeof(string))]))];

    public override SchemaMethodInfo[] GetRawConstructors(string name, SourceMetadataContext context)
    {
        EnsureName(name);
        return GetRawConstructors(context);
    }

    private static void EnsureName(string name)
    {
        if (!string.Equals(name, "rows", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"Unknown invoice source '{name}'.");
    }

    private static MethodsAggregator CreateLibrary()
    {
        var manager = new MethodsManager();
        manager.RegisterLibraries(new InvoiceLibrary());
        return new MethodsAggregator(manager);
    }
}

public sealed class InvoiceTable : ISchemaTable
{
    public ISchemaColumn[] Columns { get; } =
    [
        new SchemaColumn(nameof(Invoice.Position), 0, typeof(int)),
        new SchemaColumn(nameof(Invoice.Customer), 1, typeof(string)),
        new SchemaColumn(nameof(Invoice.Amount), 2, typeof(decimal?)),
        new SchemaColumn(nameof(Invoice.Quantity), 3, typeof(int?))
    ];
    public SchemaTableMetadata Metadata { get; } = new(typeof(Invoice));
    public ISchemaColumn? GetColumnByName(string name)
        => Columns.SingleOrDefault(column => column.ColumnName == name);
    public ISchemaColumn[] GetColumnsByName(string name)
        => Columns.Where(column => column.ColumnName == name).ToArray();
}

public sealed class InvoiceSource(string customer, SourceExecutionContext context) : RowSourceBase<Invoice>
{
    protected override void CollectChunks(IChunkWriter<Invoice> writer)
    {
        context.EndWorkToken.ThrowIfCancellationRequested();
        writer.CancellationToken.ThrowIfCancellationRequested();
        Invoice[] rows =
        [
            new(1, "A", 1.25m, 1), new(2, "A", 2.5m, 2),
            new(3, "A", null, null), new(4, "B", -1m, 4)
        ];
        writer.Write(rows.Where(row => customer == "*" || row.Customer == customer).ToArray());
    }
}

public sealed class InvoiceLibrary : LibraryBase
{
    [AggregateFunction(typeof(PositiveCountKernel), Name = "PositiveCount",
        EmptyResultBehavior = AggregateEmptyResultBehavior.Zero)]
    [WindowFunction(Name = "PositiveCount", KernelType = typeof(PositiveCountKernel),
        Modes = WindowEvaluationModes.WholePartition | WindowEvaluationModes.Running |
            WindowEvaluationModes.RecomputeFrames,
        EmptyResultBehavior = AggregateEmptyResultBehavior.Zero)]
    public long PositiveCount(decimal? amount) => AggregateFunction.NotInvoked<long>();

    public static class PositiveCountKernel
    {
        public struct State { public long Count; }
        public static void Reset(ref State state) => state = default;
        public static void Set(ref State state, decimal? amount)
        {
            if (amount.HasValue && amount.Value > 0m)
                state.Count = checked(state.Count + 1);
        }
        public static long Get(in State state) => state.Count;
    }
}

public sealed class InvoiceSchemaProvider : ISchemaProvider
{
    public ISchema GetSchema(string schema)
        => schema.TrimStart('#') == "invoice" ? new InvoiceSchema() : throw new NotSupportedException(schema);
}
```

## Executable queries and expectations

```sql
select Customer, Sum(Amount) as Total, PositiveCount(Amount) as Positive,
       Avg(Quantity) as AverageQuantity
from invoice.rows('*')
group by Customer
order by Customer
```

Expected rows: A / decimal? 3.75 / long 2 / decimal? 1.5; B / decimal? -1 / long 0 / decimal? 4.

```sql
select Position, PositiveCount(Amount) over
       (partition by Customer order by Position rows between unbounded preceding and current row) as Positive
from invoice.rows('*')
order by Position
```

Positions are int [1,2,3,4]; Positive is long [1,2,2,0]. The final row proves partition state reset. `invoice.rows('A')` yields three rows and preserves the null Amount.

## Typed execution contracts

Metadata and execution are separate. `GetTableByName`, `DescribeSource`, `DescribeSourceRuntimeSettings` and `TryPlanSource` may receive metadata `params object?[]`. They must not use that permission to transfer query values during execution.

Execution implements `GetRowSource<TRow, TArguments>(name, context, arguments)`. `TArguments` is a struct implementing `ISourceArguments`; overrides inherit the generic constraints. Use `arguments.Count` and exact `arguments.GetValue<T>(index)`, including nullable types. Explicitly construct the concrete source and use `EnsureSourceType<TRequested, TActual>`. Metadata constructors advertise signatures; the engine does not reflectively invoke them per row.

A table declares concrete columns with unique positions and `SchemaTableMetadata(typeof(Entity))`. Preserve property names, nullability and nested shapes. Unresolved object-valued columns fail with MQ3071. Empty input does not justify an unknown row type.

`RowSourceBase<T>.CollectChunks(IChunkWriter<T>)` emits bounded `IReadOnlyList<T>` chunks. Keep buffers alive while consumers can observe them. A chunk enumerator owns its producer session; early disposal cancels that session. The writer token is not automatically the host token: link `writer.CancellationToken` and `context.EndWorkToken` for cancellable I/O and dispose the linked token source. Check both for synchronous work. Do not start I/O in metadata constructors. Preserve begin/end/progress reporting around actual provider work, cancellation, exceptions and disposal.

## Library-owned aggregates and windows

The complete plugin below inherits built-ins and adds decimal-input/long-result `PositiveCount`. Both aggregate and window attributes register its typed kernel. Read declaration kinds separately; descriptors validate exact signatures. Operations never appear in `DESC FUNCTIONS` and declaration stubs must never execute.

A kernel has independent concrete `State`, `Reset(ref State)`, `Set(ref State, valueArgs...)`, `Get(in State)` and optional exact operations such as `Merge(ref State, in State)`. Use concrete overloads to bind closed generic input/result pairs. Extra SQL inputs become typed parameters; an uninferred result type is rejected. `[AggregateParent]` is engine metadata rather than a value argument.

| Function | Result | Empty/all-null input |
|---|---|---|
| Sum | Nullable input numeric type; checked integral/decimal arithmetic | null |
| Avg | Integral input: decimal?; otherwise nullable decimal/float/double input type | null |
| Min, Max | Nullable input numeric type | null |
| Count, Count(*), CountDistinct | long | zero |

Ignore numeric nulls. Integral Avg over [1, 2] is decimal 1.5. Existing distinct variants and aggregate windows follow these rules. Floating-point order and checked overflow affect results/failures; do not authorize regrouping casually.

WholePartition, Running and RecomputeFrames describe supported window evaluation. They do not grant concurrency. Typed frame/order contexts, retraction and batch operations require exact registered signatures. Composite ORDER BY keys remain concrete tuples; captured keys are not reevaluated. Ranking and QUALIFY optimizations are descriptor/planner-owned, not SQL-name switches.

Omitted function/source execution permissions mean serial execution. `ConcurrentInvocations`, `IndependentGroups`, `IndependentPartitions` and `EquivalentPartialMerge` are separate function facts; datasource `ConcurrentAccess` and `ConcurrentRowProcessing` are separate source facts. The planner intersects permissions. A merge method, purity, replayability or a stable field does not grant permission. Keep mutable state local to its group, partition, worker or receiver; never in static fields. A descendant's new declarations default to serial, including when inherited methods permit concurrency.

Scalar declarations use concrete arguments/results. Variadics use fixed overloads or `params ReadOnlySpan<T>`; expanded array arguments fail compilation. A preexisting concrete array may be passed directly. `[NonDeterministic]` constrains evaluation/reuse; it does not create a fresh receiver for each call. Library receivers belong to each query execution. Do not call MethodInfo.Invoke, DynamicInvoke or object-valued converters per row.

## Native dynamic sources and parameters

Runtime-shaped sources must resolve authoritative column/nested types before compilation. Implement `IQueryScopedRowSourceSchema`, advertise `QueryScopedRows`/`LogicalScalarReads`, and expose `QueryScopedRowContract` as metadata rather than an execution row. Execution implements:

```csharp
RowSource<TRow> GetQueryScopedRowSource<TRow, TMaterializer, TArguments>(
    string name, QueryScopedRowSourceRequest request, TArguments arguments)
    where TMaterializer : struct, IQueryRowMaterializer<TRow>
    where TArguments : struct, ISourceArguments;
```

Keep `request.Shape` and its execution context. A concrete reader implements `IQuerySourceFieldReader.Read<T>(slot)` and decodes directly into T; invoke `TMaterializer.Materialize(ref reader)` through its static constrained contract. Readers may be ref structs. Do not build object arrays, dictionary rows or boxed intermediate cells. The JSON/CSV implementations and native-ingestion guide are executable reference implementations, not permission to guess types at first-row execution.

JSON structured/mixed inferred fields use concrete JsonElement/JsonElement? in the current provider. JSON scalar helpers preserve declared/null semantics. Read modifiers belong to metadata; bind concrete decoders once, report incompatible/unsupported decoders during source description, and preserve culture/format/null behavior. TABLE/COUPLE defines a shape before compilation; it does not revive dictionary transfer.

Use `CompiledQuery.TypedParameters.Set`, `SetCollection` or `SetJson` and declared structured-input contracts. Capture independent per-run snapshots; do not mutate shared parameters during execution. Required inputs are checked before opening sources. Metadata-only DESC does not execute its described query.

## Planning, predicates and ownership

Begin with `SourcePlanResult.RejectAll(request)`, as the example does. This keeps relational work in the engine. Accept only work the provider actually performs and preserve unsupported operations as residual. Source planning is compilation metadata; a property dictionary may hold metadata but not erased row state.

Bind accepted predicates to concrete fields, literal types and nullable comparison operations before enumeration. Never interpret a predicate AST, reflect a property or use Comparer<object> per row. Unsafe OR/NOT stays residual unless the exact accepted/residual semantic partition is preserved. Fractional literals on integer fields must not be rounded into a different predicate. Accept slicing only when all preceding predicate/order dependencies are applied by the same plan. Stable columns, replayable streams and concurrency permissions are independent facts.

Preserve injected-source identity and explicit outer-join/APPLY presence. Null extension must retain the concrete nullable field type. Preserve source lifetime, cancellation and failure reporting even when the engine stops early.

Provider resource ownership remains with the provider: release streams, responses, readers and other resources acquired by the producer with `using` or `finally`, on success, failure and cancellation. Do not dispose a shared client owned by the host or caller. Enumerator disposal requests cooperative producer shutdown and waits for a bounded interval; it does not guarantee that uncooperative I/O has stopped or cleanup has finished before disposal returns. Keep I/O cancellable and release resources in the producer scope.

## Discovery, XML metadata and registration

The entrypoint assembly declares `[assembly: PluginSchemas("invoice")]`. Keep the PluginSchemas registration lowercase and identical to the schema name passed to SchemaBase. The host discovers schemas through that registration. Do not assume an arbitrary schema-provider factory name is a universal loader contract. The executable provider below is used by production compilation tests.

Generate and package the XML file alongside the entrypoint DLL. Put `<description>`, `<short-description>` and `<project-url>` on schema classes; source constructor XML uses `<virtual-constructors>`, `<virtual-constructor>`, `<examples>`, `<from>`, `<description>` and concrete `<columns>`. Dynamic columns use `isDynamic="true"`; keep static XML, runtime columns and `GetRawConstructors(SourceMetadataContext)` signatures synchronized. Use XML-safe escaped generic names. Filesystem arguments use `kind="path" relative-to="invocation-directory"` where appropriate. Runtime settings are declared through `DescribeSourceRuntimeSettings`, resolved from the execution context and must not expose secrets through help or diagnostics.

For dynamic sources, use `<columns isDynamic="true"></columns>` because a static XML scanner cannot know a shape that depends on query-local TABLE/COUPLE declarations, source arguments or external data. Explain in the XML description how callers provide the authoritative shape and which read modifiers are supported. Execution must materialize that resolved concrete shape; dynamic discovery does not authorize object-valued row transfer.

Declare settings through `DescribeSourceRuntimeSettings` returning `SourceRuntimeSettingRequirement(Name, Required, Secret, Phases, Description)`. `SourceRuntimeSettingPhase` supports `Metadata`, `Planning`, `Execution` and `All`; include every phase that actually needs the setting. For example, `new SourceRuntimeSettingRequirement("API_TOKEN", Required: true, Secret: true, Phases: SourceRuntimeSettingPhase.Execution, Description: "Token used to call the remote API.")` declares an execution-only secret. Read resolved settings from `executionContext.SourceRuntimeSettings`, validate provider-specific formats before starting I/O, and test missing required values, invalid values and phase availability. Required settings are checked before execution; never expose secret values through help, logs or diagnostics. Metadata parameter arrays in this declaration API remain compilation metadata.

## Testing and native result consumption

Successful tests follow SQL -> production compilation -> execution -> complete consumption -> explicit assertions. The example source is compiled directly into NativePluginAuthoringTests; both SQL files are read verbatim. Assert names, exact types/nullability, row count and values. Compare unordered results as multisets unless SQL requests order.

Dispose compiled queries, result tables and source enumerators. Use exact `row.GetValue<T>(columnIndex)`, including nullable types. Native rows also implement `Count` and `VisitColumns<TVisitor>(ref visitor)` where the visitor implements `IColumnValueVisitor`; serializers visit concrete values. No object cell indexer exists. Known DTO/tuple output uses `CompileForTypedExecution<TOutput>`; runtime-shaped output uses column visitation. Test-only heterogeneous assertion visitors are not production adapters.

Negative cases submit actual SQL through `CompileWithDiagnostics` and assert the responsible diagnostic/reason. MQ3071 covers invalid source contracts; MQ3088 covers unsupported generated generic constraints. Exercise null/empty inputs, custom kernels, inheritance/discovery, structured shapes, overflow, frame resets and permission intersections. Use deterministic synchronization for concurrent cases and independent expected values. Snapshots/IL/allocation checks supplement executed SQL.

Test production SQL `desc invoice`, `desc invoice.rows` and `desc invoice.rows('A')`, consuming all result rows and asserting constructor names, arguments, column names and concrete types. Verify `[assembly: PluginSchemas("invoice")]` on the entrypoint assembly and compare it with the name passed to SchemaBase. Test `GetRawConstructors(SourceMetadataContext)` independently against the executable source signatures. Parse the generated XML, verify source overloads and columns against runtime metadata, and assert that the packaged XML sits beside the entrypoint DLL. Custom aggregate/window declarations must appear as SQL signatures while kernel operations stay hidden.

## Packaging and compatibility

Use matching host ABI package references to Musoq.Schema and Musoq.Plugins only in the entrypoint project. Exclude their runtime assets; do not bundle host-owned Parser/Converter/Evaluator/Targets assemblies or reference them directly. Test projects may reference Converter/Evaluator. Follow the .NET ZIP specification for outer metadata, inner Plugin.zip, licenses, RID artifacts and generated compatibility manifests.

Resolve versions from the intended host and explicit package source; historical guide versions are not a dependency selection. Local qualification uses uniquely identified immutable packages and verified DLL hashes. A local package suffix is not the host assembly's semantic version. A compatible range alone does not prove an old host implements the breaking typed API; validate actual ABI bytes and loaders. Never overwrite package bytes under an existing version.

The sample project inherits version properties from this repository's Directory.Build.props. In a standalone repository, supply matching MusoqSchemaVersion/MusoqPluginsVersion properties and explicit NuGet sources. Build the sample with `dotnet build examples/native-plugin/Musoq.DataSources.InvoiceExample.csproj -c Release`; run its regression fixture with `dotnet test Musoq.DataSources.RepresentativeTests -c Release --filter FullyQualifiedName~NativePluginAuthoringTests`.

Packaging checks remain local. Do not install over a live user's plugins during authoring tests. Publishing, importing and creating a release are separate authorized workflows; this migration permits local commits only.

## Datasource input diagnostics

Malformed provider input is reported with `Musoq.Schema.Diagnostics.DataSourceInputException`, not a Parser diagnostic type. Its constructor accepts a stable `DataSourceInputFailureReason`, an optional `DataSourceInputLocation`, and an optional BCL inner exception. The available reasons are `MalformedInput`, `UnterminatedQuotedField`, and `MalformedJson`. Line and column values are one-based; byte offsets, character offsets and byte positions within a line are zero-based. The exception message is fixed for each reason: do not include paths, payload fragments, parser messages or other provider-controlled text in the public message. Core maps this Schema contract to MQ7014 and preserves its controlled reason and input location through datasource lifecycle diagnostics.

## Troubleshooting and migration

| Symptom | Correct action |
|---|---|
| Source override no longer compiles | Implement GetRowSource<TRow, TArguments>; read exact typed slots |
| MQ3071 erased/unknown source shape | Declare concrete fields and use a typed source or query-scoped materializer |
| Result getter type mismatch | Read the exact column CLR type, including nullable wrappers |
| DESC misses a source | Align XML, registration and GetRawConstructors metadata |
| Kernel rejected | Check exact state/ref/in signatures and concrete input/result bindings |
| Unsupported frame or generated constraint | Fix the declaration/shape; do not add an execution fallback |
| Old compiled/persisted artifact rejected | Recreate it against the accepted host contract |
| Cancellation fails during early TAKE | Dispose enumeration and observe host plus producer-session tokens |

Before examples from the previous migration are reused, replace object-row transfer, execution object arrays, boxed predicate/sort getters, SetAggregate/GetAggregate conventions and window factories. Metadata object arrays remain allowed. Preserve behavioral tests while removing old fixtures; never weaken assertions or restore a compatibility adapter to make the guide compile.

## Executable native dynamic-source example

The production JSON provider resolves authoritative scalar/structured shapes before compilation and implements native query-scoped materialization. The following SQL is also executed by NativePluginAuthoringTests. Replace `{fixture}` with the SQL-escaped path to the supplied rows.json before compilation; this placeholder is fixture preparation, not a runtime argument array.

```sql
select Id, Amount, JsonText(Meta) as Metadata,
       Sum(Amount) over (order by Id) as Total
from json.file('{fixture}')
where Id > 1
order by Id
```

The filtered result has two rows: Id long 2/3, Amount decimal? null/2.5, Metadata string containing the second JSON object/null, and Total decimal? null/2.5. JsonText preserves the input object's JSON text, including whitespace. Structured values are decoded into concrete JsonElement/JsonElement? storage before the helper runs. Projection, filtering and window execution do not use object-row transfer. The rows.json, dynamic.sql and production JsonQueryRowSource/JsonQueryRowProcessor provide the complete executable reference.
