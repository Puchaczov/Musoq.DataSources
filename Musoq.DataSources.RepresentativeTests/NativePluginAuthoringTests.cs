using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.InvoiceExample;
using Musoq.DataSources.Json;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;

namespace Musoq.DataSources.RepresentativeTests;

[TestClass]
public sealed class NativePluginAuthoringTests
{
    [TestMethod]
    public void DocumentedDynamicQuery_ShouldDecodeConcreteFieldsAndRetainPredicateAndStructuredNullSemantics()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Documentation", "rows.json").Replace("\\", "/");
        var sql = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Documentation", "dynamic.sql"))
            .Replace("{fixture}", path.Replace("'", "''", StringComparison.Ordinal), StringComparison.Ordinal);
        AuthoringSampleWriter.Capture(sql, new NativeJsonSchemaProvider(), "DynamicJson");
        using var query = InstanceCreatorHelpers.CompileForExecution(sql, Guid.NewGuid().ToString("N"),
            new NativeJsonSchemaProvider(), new Dictionary<uint, IReadOnlyDictionary<string, string>>());
        using var table = query.Run();
        var rows = table.ToArray();
        CollectionAssert.AreEqual(new[] { "Id", "Amount", "Metadata", "Total" },
            table.Columns.Select(column => column.ColumnName).ToArray());
        CollectionAssert.AreEqual(new[] { typeof(long), typeof(decimal?), typeof(string), typeof(decimal?) },
            table.Columns.Select(column => column.ColumnType).ToArray());
        Assert.AreEqual(2, rows.Length);
        CollectionAssert.AreEqual(new long[] { 2, 3 }, rows.Select(row => row.GetValue<long>(0)).ToArray());
        CollectionAssert.AreEqual(new decimal?[] { null, 2.5m }, rows.Select(row => row.GetValue<decimal?>(1)).ToArray());
        Assert.AreEqual("{\"label\": \"second\"}", rows[0].GetValue<string>(2));
        Assert.IsNull(rows[1].GetValue<string>(2));
        CollectionAssert.AreEqual(new decimal?[] { null, 2.5m }, rows.Select(row => row.GetValue<decimal?>(3)).ToArray());
    }

    private sealed class NativeJsonSchemaProvider : ISchemaProvider
    {
        public ISchema GetSchema(string name) => new JsonSchema();
    }

    [DataTestMethod]
    [DataRow("MusoqPluginsDevelopment.md")]
    [DataRow("MusoqAutonomousPluginDevelopment.md")]
    public void AuthoringGuide_ShouldEmbedTheExactCompiledPlugin(string guideName)
    {
        var guide = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Documentation", guideName)).ReplaceLineEndings("\n");
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Documentation", "Plugin.cs")).ReplaceLineEndings("\n");
        StringAssert.Contains(guide, "```csharp\n" + source + "```");
    }

    [TestMethod]
    public void DocumentedGroupedQuery_ShouldRetainNumericTypesAndCustomKernelResults()
    {
        var sql = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Documentation", "grouped.sql"));
        AuthoringSampleWriter.Capture(sql, new InvoiceSchemaProvider(), "GroupedInvoice");
        using var query = InstanceCreatorHelpers.CompileForExecution(sql, Guid.NewGuid().ToString("N"),
            new InvoiceSchemaProvider(), new Dictionary<uint, IReadOnlyDictionary<string, string>>());
        using var table = query.Run();
        var rows = table.ToArray();
        CollectionAssert.AreEqual(new[] { "Customer", "Total", "Positive", "AverageQuantity" },
            table.Columns.Select(column => column.ColumnName).ToArray());
        CollectionAssert.AreEqual(new[] { typeof(string), typeof(decimal?), typeof(long), typeof(decimal?) },
            table.Columns.Select(column => column.ColumnType).ToArray());
        Assert.AreEqual(2, rows.Length);
        Assert.AreEqual("A", rows[0].GetValue<string>(0));
        Assert.AreEqual(3.75m, rows[0].GetValue<decimal?>(1));
        Assert.AreEqual(2L, rows[0].GetValue<long>(2));
        Assert.AreEqual(1.5m, rows[0].GetValue<decimal?>(3));
        Assert.AreEqual("B", rows[1].GetValue<string>(0));
        Assert.AreEqual(-1m, rows[1].GetValue<decimal?>(1));
        Assert.AreEqual(0L, rows[1].GetValue<long>(2));
        Assert.AreEqual(4m, rows[1].GetValue<decimal?>(3));
    }

    [TestMethod]
    public void DocumentedWindowQuery_ShouldInvokeCustomKernelAndResetPartitionState()
    {
        var sql = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Documentation", "windowed.sql"));
        AuthoringSampleWriter.Capture(sql, new InvoiceSchemaProvider(), "WindowedInvoice");
        using var query = InstanceCreatorHelpers.CompileForExecution(sql, Guid.NewGuid().ToString("N"),
            new InvoiceSchemaProvider(), new Dictionary<uint, IReadOnlyDictionary<string, string>>());
        using var table = query.Run();
        var rows = table.ToArray();
        CollectionAssert.AreEqual(new[] { "Position", "Positive" }, table.Columns.Select(column => column.ColumnName).ToArray());
        CollectionAssert.AreEqual(new[] { typeof(int), typeof(long) }, table.Columns.Select(column => column.ColumnType).ToArray());
        Assert.AreEqual(4, rows.Length);
        CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, rows.Select(row => row.GetValue<int>(0)).ToArray());
        CollectionAssert.AreEqual(new long[] { 1, 2, 2, 0 }, rows.Select(row => row.GetValue<long>(1)).ToArray());
    }

    [TestMethod]
    public void DocumentedSourceArgument_ShouldFilterConcreteRowsAndPreserveNulls()
    {
        using var query = InstanceCreatorHelpers.CompileForExecution(
            "select Position, Amount from invoice.rows('A') order by Position", Guid.NewGuid().ToString("N"),
            new InvoiceSchemaProvider(), new Dictionary<uint, IReadOnlyDictionary<string, string>>());
        using var table = query.Run();
        var rows = table.ToArray();
        CollectionAssert.AreEqual(new[] { "Position", "Amount" }, table.Columns.Select(column => column.ColumnName).ToArray());
        CollectionAssert.AreEqual(new[] { typeof(int), typeof(decimal?) }, table.Columns.Select(column => column.ColumnType).ToArray());
        Assert.AreEqual(3, rows.Length);
        CollectionAssert.AreEqual(new decimal?[] { 1.25m, 2.5m, null }, rows.Select(row => row.GetValue<decimal?>(1)).ToArray());
    }

    [TestMethod]
    public void DocumentedDiscovery_ShouldExposeSqlDeclarationsAndHideKernelOperations()
    {
        using var query = InstanceCreatorHelpers.CompileForExecution(
            "desc functions invoice.rows('*')", Guid.NewGuid().ToString("N"), new InvoiceSchemaProvider(),
            new Dictionary<uint, IReadOnlyDictionary<string, string>>());
        using var table = query.Run();
        var signatures = table.Select(row => row.GetValue<string>(0)).ToArray();
        Assert.IsTrue(signatures.Any(signature => signature.Contains("PositiveCount", StringComparison.Ordinal)));
        Assert.IsFalse(signatures.Any(signature => signature.Contains("PositiveCountKernel", StringComparison.Ordinal)));
        Assert.IsFalse(signatures.Any(signature => signature.Contains(" Reset(", StringComparison.Ordinal) ||
            signature.Contains(" Set(", StringComparison.Ordinal) || signature.Contains(" Get(", StringComparison.Ordinal)));
    }
}
