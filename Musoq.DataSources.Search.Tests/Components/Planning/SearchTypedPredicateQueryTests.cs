#nullable enable
using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.DataSources.Search.Tests.Infrastructure;

namespace Musoq.DataSources.Search.Tests.Components.Planning;

[TestClass]
public sealed class SearchTypedPredicateQueryTests
{
    [TestMethod]
    public void BoundNumericPredicate_WhenRepeatedlyInvoked_AllocatesZeroBytes()
    {
        var plan = new Musoq.DataSources.Common.TypedPredicatePlan();
        var predicate = new Musoq.Schema.Optimization.SourcePredicateComparison(
            Musoq.Schema.Optimization.SourcePredicateComparisonOperator.GreaterThan,
            new Musoq.Schema.Optimization.SourcePredicateColumn(new Musoq.Schema.Optimization.SourceColumnRef("LineNumber")),
            new Musoq.Schema.Optimization.SourcePredicateLiteral(1.5m));
        plan.Bind<Musoq.DataSources.Search.Entities.SearchLine>(predicate);
        var evaluate = plan.Get<Musoq.DataSources.Search.Entities.SearchLine>()!;
        var row = new Musoq.DataSources.Search.Entities.SearchLine("a.txt", null, null, 3, null, null, 1);
        long allocated = -1;
        var matches = 0;
        Exception? failure = null;
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                for (var warmup = 0; warmup < 1000; warmup++) _ = evaluate(row);
                var before = GC.GetAllocatedBytesForCurrentThread();
                for (var index = 0; index < 20_000; index++) if (evaluate(row)) matches++;
                allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            }
            catch (Exception exception) { failure = exception; }
        });
        thread.Start();
        thread.Join();
        Assert.IsNull(failure);
        Assert.AreEqual(20_000, matches);
        Assert.AreEqual(0L, allocated);
    }

    [TestMethod]
    [DataRow("LineNumber > 1.5 and LineNumber < 8.5", "2,3,5,6,7")]
    [DataRow("LineNumber in (2,4,7)", "2,7")]
    [DataRow("Origin is null and LineNumber <= 5", "1,2,3,5")]
    [DataRow("Origin not in ('x', null)", "")]
    [DataRow("Origin = 'x' or LineNumber = 3", "3")]
    public void Query_WhenTypedPredicatesFilterNullableSearchRows_PreservesExplicitResults(string predicate, string expectedNumbers)
    {
        var root = Path.Combine(Path.GetTempPath(), "musoq-typed-search-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllLines(Path.Combine(root, "fixture.txt"), Enumerable.Range(1, 20)
                .Select(static number => string.Concat(Enumerable.Repeat("TODO ", number * 7 % 4))));
            var query = $"select LineNumber as Line, OccurrenceCount as Hits, Origin from search.lines('{root.Replace('\\', '/')}','TODO') where {predicate} order by LineNumber";
            using var compiled = InstanceCreatorHelpers.CompileForExecution(query, "TypedSearch" + Guid.NewGuid().ToString("N"),
                new SearchSchemaProvider(), EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
            var expected = expectedNumbers.Length == 0 ? Array.Empty<long>() : expectedNumbers.Split(',').Select(long.Parse).ToArray();
            for (var run = 0; run < 2; run++)
            {
                using var table = compiled.Run();
                Assert.AreEqual(expected.Length, table.Count);
                CollectionAssert.AreEqual(new[] { "Line", "Hits", "Origin" }, table.Columns.Select(static column => column.ColumnName).ToArray());
                CollectionAssert.AreEqual(new[] { typeof(long), typeof(long), typeof(string) }, table.Columns.Select(static column => column.ColumnType).ToArray());
                CollectionAssert.AreEqual(expected, table.Select(static row => row.GetValue<long>(0)).ToArray());
                CollectionAssert.AreEqual(expected.Select(static number => number * 7 % 4).ToArray(), table.Select(static row => row.GetValue<long>(1)).ToArray());
                Assert.IsTrue(table.All(static row => row.GetValue<string>(2) is null));
            }
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
