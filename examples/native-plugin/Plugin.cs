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
