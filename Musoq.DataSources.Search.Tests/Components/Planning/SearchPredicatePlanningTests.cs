#nullable enable

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema.Optimization;

using Musoq.DataSources.Search.Entities;

using Musoq.DataSources.Search.Sources;

using Musoq.DataSources.Search.Components.Planning;
using Musoq.DataSources.Search.Components.Contracts;

using Musoq.DataSources.Search.Components.Traversal;

using Musoq.DataSources.Search.Tests.Infrastructure;

namespace Musoq.DataSources.Search.Tests.Components.Planning;

[TestClass]
public sealed class SearchPredicatePlanningTests
{
    [TestMethod]
    public void Planner_ShouldAcceptScalarMetadataPredicatesForEachSearchRowShape()
    {
        var cases = new[]
        {
            ("matches", "LineNumber", (SourcePredicateExpression)GreaterOrEqual("m.LineNumber", 2L)),
            ("lines", "OccurrenceCount", (SourcePredicateExpression)In("l.OccurrenceCount", 1, 2)),
            ("files", "Path", (SourcePredicateExpression)Equal("f.Path", "a.txt")),
            ("counts", "Complete", (SourcePredicateExpression)Equal("c.Complete", true)),
            ("paths", "EntryKind", (SourcePredicateExpression)Equal("p.EntryKind", "file"))
        };

        foreach (var (source, _, predicate) in cases)
        {
            var result = Plan(source, predicate);

            Assert.AreSame(predicate, result.AcceptedPredicate, source);
            Assert.AreSame(predicate, result.ExecutionPlan.AcceptedPredicate, source);
            Assert.IsNull(result.ResidualPredicate, source);
        }
    }

    [TestMethod]
    public void Planner_ShouldSplitAndKeepUnsupportedContentResidual()
    {
        var accepted = Equal("m.Path", "a.txt");
        var residual = Equal("m.MatchText", "TODO");
        var predicate = new SourcePredicateLogical(
            SourcePredicateLogicalOperator.And,
            accepted,
            residual);

        var result = Plan("matches", predicate);

        Assert.AreSame(accepted, result.AcceptedPredicate);
        Assert.AreSame(residual, result.ResidualPredicate);
        Assert.AreSame(accepted, result.ExecutionPlan.AcceptedPredicate);
    }

    [TestMethod]
    public void Planner_ShouldKeepUnsupportedOrAndNotPredicatesWhole()
    {
        var accepted = Equal("m.Path", "a.txt");
        var unsupported = Equal("m.MatchText", "TODO");
        var or = new SourcePredicateLogical(
            SourcePredicateLogicalOperator.Or,
            accepted,
            unsupported);

        var orResult = Plan("matches", or);
        Assert.IsNull(orResult.AcceptedPredicate);
        Assert.AreSame(or, orResult.ResidualPredicate);

        var not = new UnsupportedNotPredicate(accepted);
        var notResult = Plan("matches", not);
        Assert.IsNull(notResult.AcceptedPredicate);
        Assert.AreSame(not, notResult.ResidualPredicate);
    }

    [TestMethod]
    public void Planner_ShouldKeepIsNullResidualAndAcceptOnlyIsNotNull()
    {
        var isNull = IsNull("m.Path");
        var isNullResult = Plan("matches", isNull);
        Assert.IsNull(isNullResult.AcceptedPredicate);
        Assert.AreSame(isNull, isNullResult.ResidualPredicate);

        var isNotNull = IsNotNull("m.Path");
        var isNotNullResult = Plan("matches", isNotNull);
        Assert.AreSame(isNotNull, isNotNullResult.AcceptedPredicate);
        Assert.IsNull(isNotNullResult.ResidualPredicate);
    }

    [TestMethod]
    public void Evaluator_ShouldPreserveNullThreeValuedPredicateSemantics()
    {
        var row = new SearchLine("a.txt", null, null, 1, null, null, 1);
        var isNull = IsNull("l.Origin");
        var isNotNull = new SourcePredicateNullCheck(
            new SourcePredicateColumn(new SourceColumnRef("l.Origin")),
            IsNegated: true);
        var ordinaryNullComparison = Equal("l.Origin", null);
        var notInWithNull = new SourcePredicateIn(
            new SourcePredicateColumn(new SourceColumnRef("l.Origin")),
            [new SourcePredicateLiteral("other"), new SourcePredicateLiteral(null)],
            IsNegated: true);

        Assert.IsTrue(SearchPredicateEvaluator.Matches(
            isNull,
            row,
            SearchPredicateValues.GetLineValue));
        Assert.IsFalse(SearchPredicateEvaluator.Matches(
            isNotNull,
            row,
            SearchPredicateValues.GetLineValue));
        Assert.IsFalse(SearchPredicateEvaluator.Matches(
            ordinaryNullComparison,
            row,
            SearchPredicateValues.GetLineValue));
        Assert.IsFalse(SearchPredicateEvaluator.Matches(
            notInWithNull,
            row,
            SearchPredicateValues.GetLineValue));
    }

    [TestMethod]
    public void Evaluator_ShouldHandleAliasesReversedComparisonsAndNumericLiteralConversions()
    {
        var predicate = new SourcePredicateComparison(
            SourcePredicateComparisonOperator.LessThan,
            new SourcePredicateLiteral(2),
            new SourcePredicateColumn(new SourceColumnRef("m.LineNumber")));
        var row = new SearchMatch("a.txt", 0, 3, 0, "TODO");

        var result = Plan("matches", predicate);

        Assert.AreSame(predicate, result.AcceptedPredicate);
        Assert.IsTrue(SearchPredicateEvaluator.Matches(
            predicate,
            row,
            SearchPredicateValues.GetMatchValue));
        Assert.IsFalse(SearchPredicateEvaluator.Matches(
            predicate,
            new SearchMatch("a.txt", 0, 2, 0, "TODO"),
            SearchPredicateValues.GetMatchValue));
    }

    [TestMethod]
    public void AcceptedPredicates_ShouldMatchRejectAllBaselineOnGeneratedFixtures()
    {
        var root = CreateTemporaryRoot();
        try
        {
            Write(root, "first.txt", "TODO\nTODO TODO\n");
            Write(root, "second.txt", "TODO\n");

            var matchPredicate = GreaterOrEqual("m.LineNumber", 2);
            var baselineMatches = new SearchMatchesSource(
                    root,
                    "TODO",
                    RuntimeV2TestContexts.CreateExecutionContext())
                .Chunks
                .SelectMany(static chunk => chunk)
                .Where(row => SearchPredicateEvaluator.Matches(
                    matchPredicate,
                    row,
                    SearchPredicateValues.GetMatchValue))
                .Select(static row => $"{row.Path}:{row.MatchIndex}")
                .OrderBy(static value => value, StringComparer.Ordinal)
                .ToArray();
            var acceptedMatches = new SearchMatchesSource(
                    SearchRequest.Create(root, "TODO"),
                    RuntimeV2TestContexts.CreateExecutionContext(
                        executionPlan: ExecutionPlan(matchPredicate)))
                .Chunks
                .SelectMany(static chunk => chunk)
                .Select(static row => $"{row.Path}:{row.MatchIndex}")
                .OrderBy(static value => value, StringComparer.Ordinal)
                .ToArray();

            CollectionAssert.AreEqual(baselineMatches, acceptedMatches);

            var countPredicate = GreaterOrEqual("c.OccurrenceCount", 1);
            var baselineCounts = new SearchCountsSource(
                    root,
                    "TODO",
                    RuntimeV2TestContexts.CreateExecutionContext())
                .Chunks
                .SelectMany(static chunk => chunk)
                .Where(row => SearchPredicateEvaluator.Matches(
                    countPredicate,
                    row,
                    SearchPredicateValues.GetCountValue))
                .Select(static row => row.Path)
                .OrderBy(static value => value, StringComparer.Ordinal)
                .ToArray();
            var acceptedCounts = new SearchCountsSource(
                    SearchRequest.Create(root, "TODO"),
                    RuntimeV2TestContexts.CreateExecutionContext(
                        executionPlan: ExecutionPlan(countPredicate)))
                .Chunks
                .SelectMany(static chunk => chunk)
                .Select(static row => row.Path)
                .OrderBy(static value => value, StringComparer.Ordinal)
                .ToArray();

            CollectionAssert.AreEqual(baselineCounts, acceptedCounts);

            var linePredicate = GreaterOrEqual("l.LineNumber", 2);
            var baselineLines = new SearchLinesSource(
                    root,
                    "TODO",
                    RuntimeV2TestContexts.CreateExecutionContext())
                .Chunks
                .SelectMany(static chunk => chunk)
                .Where(row => SearchPredicateEvaluator.Matches(
                    linePredicate,
                    row,
                    SearchPredicateValues.GetLineValue))
                .Select(static row => $"{row.Path}:{row.LineNumber}")
                .OrderBy(static value => value, StringComparer.Ordinal)
                .ToArray();
            var acceptedLines = new SearchLinesSource(
                    SearchRequest.Create(root, "TODO"),
                    RuntimeV2TestContexts.CreateExecutionContext(
                        executionPlan: ExecutionPlan(linePredicate)))
                .Chunks
                .SelectMany(static chunk => chunk)
                .Select(static row => $"{row.Path}:{row.LineNumber}")
                .OrderBy(static value => value, StringComparer.Ordinal)
                .ToArray();

            CollectionAssert.AreEqual(baselineLines, acceptedLines);

            var filePredicate = Equal("f.Path", "first.txt");
            var acceptedFiles = new SearchFilesSource(
                    SearchRequest.Create(root, "TODO"),
                    RuntimeV2TestContexts.CreateExecutionContext(
                        executionPlan: ExecutionPlan(filePredicate)))
                .Chunks
                .SelectMany(static chunk => chunk)
                .Select(static row => row.Path)
                .ToArray();
            CollectionAssert.AreEqual(new[] { "first.txt" }, acceptedFiles);

            var pathPredicate = Equal("p.Path", "second.txt");
            var acceptedPaths = new SearchPathsSource(
                    root,
                    RuntimeV2TestContexts.CreateExecutionContext(
                        executionPlan: ExecutionPlan(pathPredicate)),
                    ScopePolicy.Default,
                    new SearchScopeCounters())
                .Chunks
                .SelectMany(static chunk => chunk)
                .Select(static row => row.Path)
                .ToArray();
            CollectionAssert.AreEqual(new[] { "second.txt" }, acceptedPaths);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void CompiledOuterJoin_ShouldRetainResidualPredicateSemantics()
    {
        var root = CreateTemporaryRoot();
        try
        {
            Write(root, "alpha.txt", "TODO\n");
            Write(root, "beta.txt", "DONE\n");
            var escapedRoot = root.Replace("\\", "\\\\", StringComparison.Ordinal);
            var query =
                $"select p.Path from search.paths('{escapedRoot}') p " +
                $"left outer join search.matches('{escapedRoot}', 'TODO') m on p.Path = m.Path " +
                "where m.Path = 'alpha.txt' order by p.Path";

            var result = InstanceCreatorHelpers.CompileForExecution(
                    query,
                    Guid.NewGuid().ToString(),
                    new SearchSchemaProvider(),
                    EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables())
                .Run();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("alpha.txt", result.Rows[0][0]);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    private static SourcePlanResult Plan(
        string source,
        SourcePredicateExpression predicate)
    {
        return new SearchSchema().TryPlanSource(
            source,
            new SourcePlanRequest
            {
                Identity = new SourceIdentity("search", source, "tests", "predicate"),
                Predicate = predicate
            });
    }

    private static SourceExecutionPlan ExecutionPlan(SourcePredicateExpression predicate)
    {
        return new SourceExecutionPlan
        {
            Identity = new SourceIdentity("search", "matches", "tests", "predicate"),
            AcceptedPredicate = predicate
        };
    }

    private static SourcePredicateComparison Equal(string column, object? value)
    {
        return new SourcePredicateComparison(
            SourcePredicateComparisonOperator.Equal,
            new SourcePredicateColumn(new SourceColumnRef(column)),
            new SourcePredicateLiteral(value));
    }

    private static SourcePredicateComparison GreaterOrEqual(string column, object value)
    {
        return new SourcePredicateComparison(
            SourcePredicateComparisonOperator.GreaterOrEqual,
            new SourcePredicateColumn(new SourceColumnRef(column)),
            new SourcePredicateLiteral(value));
    }

    private static SourcePredicateNullCheck IsNull(string column)
    {
        return new SourcePredicateNullCheck(
            new SourcePredicateColumn(new SourceColumnRef(column)));
    }

    private static SourcePredicateNullCheck IsNotNull(string column)
    {
        return new SourcePredicateNullCheck(
            new SourcePredicateColumn(new SourceColumnRef(column)),
            IsNegated: true);
    }

    private static SourcePredicateIn In(string column, params object?[] values)
    {
        return new SourcePredicateIn(
            new SourcePredicateColumn(new SourceColumnRef(column)),
            values.Select(static value => (SourcePredicateExpression)new SourcePredicateLiteral(value)).ToArray());
    }

    private static string CreateTemporaryRoot()
    {
        return Path.Combine(Path.GetTempPath(), $"musoq-search-predicate-{Guid.NewGuid():N}");
    }

    private static void Write(string root, string relativePath, string content)
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, relativePath), content);
    }

    private static void DeleteTemporaryRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    private sealed record UnsupportedNotPredicate(SourcePredicateExpression Operand)
        : SourcePredicateExpression;

}
