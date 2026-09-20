#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Schema.Optimization;

using Musoq.DataSources.Search.Sources;

using Musoq.DataSources.Search.Components.Contracts;

using Musoq.DataSources.Search.Tests.Infrastructure;

namespace Musoq.DataSources.Search.Tests.Components.Planning;

[TestClass]
public sealed class SearchOrderingAndSlicingTests
{
    [TestMethod]
    public void Planner_ShouldAcceptSliceWhenPredicateAndOrderAreFullySatisfied()
    {
        var predicate = Compare(
            SourcePredicateComparisonOperator.GreaterOrEqual,
            "m.LineNumber",
            2L);
        var result = Plan(
            predicate,
            orderBy: [],
            skip: 1,
            take: 2);

        Assert.AreSame(predicate, result.AcceptedPredicate);
        Assert.IsNull(result.ResidualPredicate);
        Assert.AreEqual(1L, result.AcceptedSkip);
        Assert.IsNull(result.ResidualSkip);
        Assert.AreEqual(2L, result.AcceptedTake);
        Assert.IsNull(result.ResidualTake);
        Assert.AreEqual(result.AcceptedSkip, result.ExecutionPlan.AcceptedSkip);
        Assert.AreEqual(result.AcceptedTake, result.ExecutionPlan.AcceptedTake);
    }

    [TestMethod]
    public void Planner_ShouldKeepSliceResidualWhenPredicateOrOrderIsNotFullySatisfied()
    {
        var predicate = Compare(
            SourcePredicateComparisonOperator.Equal,
            "m.MatchText",
            "TODO");
        var order = new OrderByExpression(
            new SourceColumnRef("m.Path"),
            OrderDirection.Descending);
        var result = Plan(
            predicate,
            orderBy: [order],
            skip: 1,
            take: 2);

        Assert.IsNull(result.AcceptedPredicate);
        Assert.AreSame(predicate, result.ResidualPredicate);
        Assert.AreEqual(0, result.AcceptedOrderBy.Count);
        CollectionAssert.AreEqual(new[] { order }, result.ResidualOrderBy.ToArray());
        Assert.IsNull(result.AcceptedSkip);
        Assert.AreEqual(1L, result.ResidualSkip);
        Assert.IsNull(result.AcceptedTake);
        Assert.AreEqual(2L, result.ResidualTake);
        Assert.IsFalse(result.ExecutionPlan.AcceptedSkip.HasValue);
        Assert.IsFalse(result.ExecutionPlan.AcceptedTake.HasValue);
    }

    [TestMethod]
    public void AcceptedSlice_ShouldApplyAfterAcceptedPredicate()
    {
        var root = CreateTemporaryRoot();
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "fixture.txt"), "TODO TODO TODO\n");
            var predicate = Compare(
                SourcePredicateComparisonOperator.GreaterOrEqual,
                "m.MatchIndex",
                1L);
            var plan = new SourceExecutionPlan
            {
                Identity = Identity(),
                AcceptedPredicate = predicate,
                AcceptedSkip = 1,
                AcceptedTake = 1
            };

            var rows = new SearchMatchesSource(
                    SearchRequest.Create(root, "TODO"),
                    RuntimeV2TestContexts.CreateExecutionContext(executionPlan: plan))
                .Chunks
                .SelectMany(static chunk => chunk)
                .ToArray();

            Assert.AreEqual(1, rows.Length);
            Assert.AreEqual(2L, rows[0].MatchIndex);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void AcceptedTake_ShouldBeGlobalAcrossFileSinks()
    {
        var root = CreateTemporaryRoot();
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "first.txt"), "TODO\n");
            File.WriteAllText(Path.Combine(root, "second.txt"), "TODO\n");
            var plan = new SourceExecutionPlan
            {
                Identity = Identity(),
                AcceptedTake = 1
            };

            var rows = new SearchMatchesSource(
                    SearchRequest.Create(root, "TODO"),
                    RuntimeV2TestContexts.CreateExecutionContext(executionPlan: plan))
                .Chunks
                .SelectMany(static chunk => chunk)
                .ToArray();

            Assert.AreEqual(1, rows.Length);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void CompiledResidualFilter_ShouldRunBeforeTake()
    {
        var root = CreateTemporaryRoot();
        var escapedRoot = Escape(root);
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "fixture.txt"), "first TODO\nsecond TODO");

            var result = Compile(
                    $"select l.LineNumber from search.lines('{escapedRoot}', 'TODO') l " +
                    "where l.LineText = 'second TODO' take 1")
                .Run();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(2L, result.Rows[0][0]);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void CompiledDescendingGlobalOrder_ShouldRemainCorrect()
    {
        var root = CreateTemporaryRoot();
        var escapedRoot = Escape(root);
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "alpha.txt"), "TODO TODO\n");
            File.WriteAllText(Path.Combine(root, "beta.txt"), "TODO\n");

            var result = Compile(
                    $"select m.Path, m.MatchIndex from search.matches('{escapedRoot}', 'TODO') m " +
                    "order by m.Path desc, m.MatchIndex desc")
                .Run();

            CollectionAssert.AreEqual(
                new[] { "beta.txt:0", "alpha.txt:1", "alpha.txt:0" },
                result.Rows.Select(row => $"{row[0]}:{row[1]}").ToArray());
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void CompiledOrderedQuery_ShouldRemainDeterministicAcrossParallelRuns()
    {
        var root = CreateTemporaryRoot();
        var escapedRoot = Escape(root);
        try
        {
            Directory.CreateDirectory(root);
            var expected = new List<string>();
            for (var fileIndex = 0; fileIndex < 12; fileIndex++)
            {
                var path = $"file-{fileIndex:D2}.txt";
                var occurrenceCount = fileIndex % 4 + 1;
                File.WriteAllText(
                    Path.Combine(root, path),
                    string.Concat(Enumerable.Repeat("TODO ", occurrenceCount)));
                for (var matchIndex = 0; matchIndex < occurrenceCount; matchIndex++)
                    expected.Add($"{path}:{matchIndex}");
            }

            string[]? first = null;
            for (var run = 0; run < 3; run++)
            {
                var result = Compile(
                        $"select m.Path, m.MatchIndex from search.matches('{escapedRoot}', 'TODO') m " +
                        "order by m.Path, m.MatchIndex")
                    .Run();
                var actual = result.Rows
                    .Select(row => $"{row[0]}:{row[1]}")
                    .ToArray();

                CollectionAssert.AreEqual(expected.ToArray(), actual);
                first ??= actual;
                CollectionAssert.AreEqual(first, actual);
            }
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void CompiledTieOrder_ShouldRetainEveryOccurrence()
    {
        var root = CreateTemporaryRoot();
        var escapedRoot = Escape(root);
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "fixture.txt"), "TODO TODO\n");

            var result = Compile(
                    $"select m.MatchIndex, m.LineNumber from search.matches('{escapedRoot}', 'TODO') m " +
                    "order by m.LineNumber")
                .Run();

            Assert.AreEqual(2, result.Count);
            CollectionAssert.AreEqual(
                new long[] { 0, 1 },
                result.Rows.Select(row => (long)row[0]).ToArray());
            Assert.IsTrue(result.Rows.All(row => (long)row[1] == 1));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void CompiledTakeZero_ShouldReturnNoRows()
    {
        var root = CreateTemporaryRoot();
        var escapedRoot = Escape(root);
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "fixture.txt"), "TODO\n");

            var result = Compile(
                    $"select m.Path from search.matches('{escapedRoot}', 'TODO') m take 0")
                .Run();

            Assert.AreEqual(0, result.Count);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void CompiledTakeAfterJoin_ShouldNotTruncateJoinInputs()
    {
        var root = CreateTemporaryRoot();
        var escapedRoot = Escape(root);
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "first.txt"), "NONE\n");
            File.WriteAllText(Path.Combine(root, "second.txt"), "NONE\n");

            var sourceOrder = new SearchPathsSource(
                    root,
                    RuntimeV2TestContexts.CreateExecutionContext())
                .Chunks
                .SelectMany(static chunk => chunk)
                .Select(static row => row.Path)
                .ToArray();
            Assert.AreEqual(2, sourceOrder.Length);

            File.WriteAllText(
                Path.Combine(root, sourceOrder[1].Replace('/', Path.DirectorySeparatorChar)),
                "TODO\n");

            var result = Compile(
                    $"select p.Path, m.Path from search.paths('{escapedRoot}') p " +
                    $"inner join search.matches('{escapedRoot}', 'TODO') m on p.Path = m.Path " +
                    "take 1")
                .Run();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(sourceOrder[1], result.Rows[0][0]);
            Assert.AreEqual(sourceOrder[1], result.Rows[0][1]);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    private static SourcePlanResult Plan(
        SourcePredicateExpression? predicate,
        IReadOnlyList<OrderByExpression> orderBy,
        long? skip,
        long? take)
    {
        return new SearchSchema().TryPlanSource(
            "matches",
            new SourcePlanRequest
            {
                Identity = Identity(),
                Predicate = predicate,
                OrderBy = orderBy,
                Skip = skip,
                Take = take
            });
    }

    private static SourcePredicateComparison Compare(
        SourcePredicateComparisonOperator op,
        string column,
        object? value)
    {
        return new SourcePredicateComparison(
            op,
            new SourcePredicateColumn(new SourceColumnRef(column)),
            new SourcePredicateLiteral(value));
    }

    private static SourceIdentity Identity()
    {
        return new SourceIdentity("search", "matches", "ordering-tests", "m");
    }

    private static CompiledQuery Compile(string query)
    {
        return InstanceCreatorHelpers.CompileForExecution(
            query,
            Guid.NewGuid().ToString(),
            new SearchSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    private static string CreateTemporaryRoot()
    {
        return Path.Combine(Path.GetTempPath(), $"musoq-search-ordering-{Guid.NewGuid():N}");
    }

    private static string Escape(string path)
    {
        return path.Replace("\\", "\\\\", StringComparison.Ordinal);
    }

    private static void DeleteTemporaryRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}
