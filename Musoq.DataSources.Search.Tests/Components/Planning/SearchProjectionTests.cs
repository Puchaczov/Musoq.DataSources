#nullable enable

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

using Musoq.DataSources.Search.Entities;

using Musoq.DataSources.Search.Helpers;
using Musoq.DataSources.Search.Sources;

using Musoq.DataSources.Search.Components.Contracts;

using Musoq.DataSources.Search.Components.Planning;

using Musoq.DataSources.Search.Components.Text;

using Musoq.DataSources.Search.Tests.Infrastructure;

namespace Musoq.DataSources.Search.Tests.Components.Planning;

[TestClass]
public sealed class SearchProjectionTests
{
    [TestMethod]
    public void Planner_ShouldCarryRequiredColumnsIntoExecutionPlan()
    {
        var requested = new SourceColumnRef[]
        {
            new("m.Path"),
            new("m.MatchText"),
            new("m.Context")
        };
        var result = new SearchSchema().TryPlanSource(
            "matches",
            new SourcePlanRequest
            {
                Identity = new SourceIdentity("search", "matches", "projection", "m"),
                RequiredColumns = requested,
                Predicate = new SourcePredicateComparison(
                    SourcePredicateComparisonOperator.Equal,
                    new SourcePredicateColumn(new SourceColumnRef("m.MatchText")),
                    new SourcePredicateLiteral("TODO")),
                OrderBy =
                [
                    new OrderByExpression(
                        new SourceColumnRef("m.Context"),
                        OrderDirection.Ascending)
                ]
            });

        CollectionAssert.AreEqual(
            new[] { "Path", "MatchText", "Context" },
            result.AcceptedColumns.Select(static column => column.Name).ToArray());
        CollectionAssert.AreEqual(
            new[] { "Path", "MatchText", "Context" },
            result.ExecutionPlan.AcceptedColumns.Select(static column => column.Name).ToArray());
        Assert.IsNotNull(result.ResidualPredicate);
        Assert.AreEqual(1, result.ResidualOrderBy.Count);
    }

    [TestMethod]
    public void ProjectionRequirements_ShouldPreferPlanColumnsOverInferredSchema()
    {
        var requirements = SearchProjectionRequirements.From(
            RuntimeV2TestContexts.CreateExecutionContext(
                allColumns: SearchMatchesHelperColumns(),
                executionPlan: new SourceExecutionPlan
                {
                    Identity = new SourceIdentity("search", "matches", "projection", "m"),
                    AcceptedColumns = [new SourceColumnRef(nameof(SearchMatch.Path))]
                }));

        Assert.IsFalse(requirements.RetainMatchText);
        Assert.IsFalse(requirements.RetainCaptures);
        Assert.IsFalse(requirements.RetainContext);
        Assert.IsFalse(requirements.RetainLineText);
    }

    [TestMethod]
    public void MatchProjection_ShouldElideTextWithoutChangingOccurrencesOrCoordinates()
    {
        var root = CreateTemporaryRoot();
        var path = Path.Combine(root, "fixture.txt");
        const string content = "TODO x TODO\nTODO\n";

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(path, content);

            var request = SearchRequest.Create(root, "TODO");
            var compactRows = ReadMatches(
                request,
                CreateContext(
                    nameof(SearchMatch.Path),
                    nameof(SearchMatch.MatchIndex),
                    nameof(SearchMatch.ByteOffset),
                    nameof(SearchMatch.ByteLength),
                    nameof(SearchMatch.LineNumber),
                    nameof(SearchMatch.Utf16Column),
                    nameof(SearchMatch.Utf16Length)));
            var textRows = ReadMatches(
                request,
                CreateContext(
                    nameof(SearchMatch.Path),
                    nameof(SearchMatch.MatchIndex),
                    nameof(SearchMatch.ByteOffset),
                    nameof(SearchMatch.ByteLength),
                    nameof(SearchMatch.LineNumber),
                    nameof(SearchMatch.Utf16Column),
                    nameof(SearchMatch.Utf16Length),
                    nameof(SearchMatch.MatchText)));

            Assert.AreEqual(3, compactRows.Length);
            Assert.IsTrue(compactRows.All(static row => row.MatchText is null));
            Assert.IsTrue(textRows.All(static row => row.MatchText == "TODO"));
            CollectionAssert.AreEqual(
                textRows.Select(SignatureWithoutText).ToArray(),
                compactRows.Select(SignatureWithoutText).ToArray());
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void LineProjection_ShouldElideTextButRetainPhysicalBoundariesAndCounts()
    {
        var root = CreateTemporaryRoot();
        const string content = "TODO TODO\nx TODO\n";

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "fixture.txt"), content);

            var request = SearchRequest.Create(root, "TODO");
            var compactRows = ReadLines(
                request,
                CreateContext(
                    nameof(SearchLine.Path),
                    nameof(SearchLine.Origin),
                    nameof(SearchLine.PatternId),
                    nameof(SearchLine.LineNumber),
                    nameof(SearchLine.ByteOffset),
                    nameof(SearchLine.OccurrenceCount)));
            var textRows = ReadLines(
                request,
                CreateContext(
                    nameof(SearchLine.Path),
                    nameof(SearchLine.Origin),
                    nameof(SearchLine.PatternId),
                    nameof(SearchLine.LineNumber),
                    nameof(SearchLine.ByteOffset),
                    nameof(SearchLine.LineText),
                    nameof(SearchLine.OccurrenceCount)));

            Assert.AreEqual(2, compactRows.Length);
            Assert.IsTrue(compactRows.All(static row => row.LineText is null));
            CollectionAssert.AreEqual(
                new long[] { 1, 2 },
                compactRows.Select(static row => row.LineNumber).ToArray());
            CollectionAssert.AreEqual(
                new long[] { 2, 1 },
                compactRows.Select(static row => row.OccurrenceCount).ToArray());
            CollectionAssert.AreEqual(
                new long?[] { 0, 10 },
                compactRows.Select(static row => row.ByteOffset).ToArray());
            CollectionAssert.AreEqual(
                textRows.Select(SignatureWithoutText).ToArray(),
                compactRows.Select(SignatureWithoutText).ToArray());
            CollectionAssert.AreEqual(
                new[] { "TODO TODO\n", "x TODO\n" },
                textRows.Select(static row => row.LineText).ToArray());
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void CompiledProjection_ShouldPreserveMultiplicityAndCoordinates()
    {
        var root = CreateTemporaryRoot();
        const string content = "TODO x TODO\nTODO\n";
        var escapedRoot = root.Replace("\\", "\\\\", StringComparison.Ordinal);

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "fixture.txt"), content);

            var compact = Compile(
                    $"select Path, MatchIndex, ByteOffset, ByteLength, LineNumber, Utf16Column, Utf16Length " +
                    $"from search.matches('{escapedRoot}', 'TODO') order by MatchIndex")
                .Run();
            var withText = Compile(
                    $"select Path, MatchIndex, ByteOffset, ByteLength, LineNumber, Utf16Column, Utf16Length, MatchText " +
                    $"from search.matches('{escapedRoot}', 'TODO') order by MatchIndex")
                .Run();

            Assert.AreEqual(3, compact.Count);
            Assert.AreEqual(3, withText.Count);
            CollectionAssert.AreEqual(
                withText.Rows
                    .Select(row => string.Join(
                        '|',
                        Enumerable.Range(0, 7).Select(index => row[index])))
                    .ToArray(),
                compact.Rows
                    .Select(row => string.Join(
                        '|',
                        Enumerable.Range(0, 7).Select(index => row[index])))
                    .ToArray());
            Assert.IsTrue(withText.Rows.All(row => (string)row[7]! == "TODO"));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void CompiledResidualWhere_ShouldMaterializeTextNeededOnlyByFilter()
    {
        var root = CreateTemporaryRoot();
        var escapedRoot = root.Replace("\\", "\\\\", StringComparison.Ordinal);

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "fixture.txt"), "TODO x TODO\n");

            var result = Compile(
                    $"select m.Path from search.matches('{escapedRoot}', 'TODO') m " +
                    "where m.MatchText = 'TODO' order by m.MatchIndex")
                .Run();

            Assert.AreEqual(2, result.Count);
            CollectionAssert.AreEqual(
                new[] { "fixture.txt", "fixture.txt" },
                result.Rows.Select(static row => (string)row[0]!).ToArray());
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void ContextRequiredOnlyByResidualOrder_ShouldMaterializeBeforeEmission()
    {
        var root = CreateTemporaryRoot();
        var plan = new SearchSchema().TryPlanSource(
            "matches",
            new SourcePlanRequest
            {
                Identity = new SourceIdentity("search", "matches", "projection-order", "m"),
                RequiredColumns = [new SourceColumnRef(nameof(SearchMatch.Context))],
                OrderBy =
                [
                    new OrderByExpression(
                        new SourceColumnRef(nameof(SearchMatch.Context)),
                        OrderDirection.Ascending)
                ]
            }).ExecutionPlan;

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "fixture.txt"), "before\nTODO\nafter\n");

            var rows = ReadMatches(
                SearchRequest.Create(
                    root,
                    "TODO",
                    context: new SearchContextOptions(
                        beforeLines: 1,
                        afterLines: 1,
                        maxBytes: 64)),
                CreateContext(
                    plan,
                    nameof(SearchMatch.Path)));

            Assert.AreEqual(1, rows.Length);
            CollectionAssert.AreEqual(
                new[] { "before\n", "after\n" },
                rows[0].Context.Select(static line => line.LineText).ToArray());
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void CompiledSelectStar_ShouldRetainItsDeclaredOutputFields()
    {
        var root = CreateTemporaryRoot();
        var escapedRoot = root.Replace("\\", "\\\\", StringComparison.Ordinal);

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "fixture.txt"), "TODO\n");

            var result = Compile(
                    $"select * from search.matches('{escapedRoot}', 'TODO')")
                .Run();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("TODO", result.Rows[0][8]);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void CompiledPathOnlyProjection_ShouldPreserveRepeatedOccurrences()
    {
        var root = CreateTemporaryRoot();
        var escapedRoot = root.Replace("\\", "\\\\", StringComparison.Ordinal);

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "fixture.txt"), "TODO TODO\n");

            var result = Compile(
                    $"select m.Path from search.matches('{escapedRoot}', 'TODO') m " +
                    "order by m.MatchIndex")
                .Run();

            Assert.AreEqual(2, result.Count);
            CollectionAssert.AreEqual(
                new[] { "fixture.txt", "fixture.txt" },
                result.Rows.Select(static row => (string)row[0]!).ToArray());
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void EmptyColumnContext_ShouldRetainDirectSourceCompatibility()
    {
        var requirements = SearchProjectionRequirements.From(
            RuntimeV2TestContexts.CreateExecutionContext());

        Assert.IsTrue(requirements.RetainMatchText);
        Assert.IsTrue(requirements.RetainLineText);
        Assert.IsTrue(requirements.RetainCaptures);
        Assert.IsTrue(requirements.RetainContext);
    }

    private static SearchMatch[] ReadMatches(
        SearchRequest request,
        SourceExecutionContext context)
    {
        return new SearchMatchesSource(request, context)
            .Chunks
            .SelectMany(static chunk => chunk)
            .ToArray();
    }

    private static SearchLine[] ReadLines(
        SearchRequest request,
        SourceExecutionContext context)
    {
        return new SearchLinesSource(request, context)
            .Chunks
            .SelectMany(static chunk => chunk)
            .ToArray();
    }

    private static SourceExecutionContext CreateContext(params string[] columns)
    {
        return RuntimeV2TestContexts.CreateExecutionContext(
            allColumns: columns
                .Select((name, index) => (ISchemaColumn)new SchemaColumn(name, index, typeof(object)))
                .ToArray());
    }

    private static SourceExecutionContext CreateContext(
        SourceExecutionPlan plan,
        params string[] columns)
    {
        return RuntimeV2TestContexts.CreateExecutionContext(
            allColumns: columns
                .Select((name, index) => (ISchemaColumn)new SchemaColumn(name, index, typeof(object)))
                .ToArray(),
            executionPlan: plan);
    }

    private static ISchemaColumn[] SearchMatchesHelperColumns()
    {
        return SearchMatchesHelper.Columns;
    }

    private static string SignatureWithoutText(SearchMatch row)
    {
        return string.Join(
            '|',
            row.Path,
            row.MatchIndex,
            row.ByteOffset,
            row.ByteLength,
            row.LineNumber,
            row.Utf16Column,
            row.Utf16Length);
    }

    private static string SignatureWithoutText(SearchLine row)
    {
        return string.Join(
            '|',
            row.Path,
            row.Origin,
            row.PatternId,
            row.LineNumber,
            row.ByteOffset,
            row.OccurrenceCount);
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
        return Path.Combine(Path.GetTempPath(), $"musoq-search-projection-{Guid.NewGuid():N}");
    }

    private static void DeleteTemporaryRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}
