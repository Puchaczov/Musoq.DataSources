#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

using Musoq.DataSources.Search.Entities;

using Musoq.DataSources.Search.Sources;

using Musoq.DataSources.Search.Components.Contracts;

using Musoq.DataSources.Search.Components.Planning;

using Musoq.DataSources.Search.Components.Text;

using Musoq.DataSources.Search.Tests.Infrastructure;

namespace Musoq.DataSources.Search.Tests.Components.Planning;

[TestClass]
public sealed class SearchPlannerDifferentialTests
{
    private const string Literal = "TODO";
    private static readonly IReadOnlyList<string> MatchColumns =
    [
        nameof(SearchMatch.Path),
        nameof(SearchMatch.PatternId),
        nameof(SearchMatch.MatchIndex),
        nameof(SearchMatch.ByteOffset),
        nameof(SearchMatch.ByteLength),
        nameof(SearchMatch.LineNumber),
        nameof(SearchMatch.Utf16Column),
        nameof(SearchMatch.Utf16Length),
        nameof(SearchMatch.MatchText),
        nameof(SearchMatch.Captures),
        nameof(SearchMatch.Context)
    ];
    private static readonly IReadOnlyList<string> LineColumns =
    [
        nameof(SearchLine.Path),
        nameof(SearchLine.Origin),
        nameof(SearchLine.PatternId),
        nameof(SearchLine.LineNumber),
        nameof(SearchLine.ByteOffset),
        nameof(SearchLine.LineText),
        nameof(SearchLine.OccurrenceCount)
    ];
    private static readonly IReadOnlyList<string> FileColumns =
    [
        nameof(SearchFile.Path),
        nameof(SearchFile.Origin),
        nameof(SearchFile.PatternId)
    ];
    private static readonly IReadOnlyList<string> CountColumns =
    [
        nameof(SearchCount.Path),
        nameof(SearchCount.Origin),
        nameof(SearchCount.PatternId),
        nameof(SearchCount.OccurrenceCount),
        nameof(SearchCount.MatchingLineCount),
        nameof(SearchCount.BytesScanned),
        nameof(SearchCount.Complete)
    ];
    private static readonly IReadOnlyList<string> PathColumns =
    [
        nameof(SearchPath.Path),
        nameof(SearchPath.Origin),
        nameof(SearchPath.EntryKind)
    ];

    [TestMethod]
    public void Planner_DifferentialMatrix_ShouldMatchRejectAllBaseline()
    {
        var root = CreateTemporaryRoot();
        try
        {
            CreateFixture(root);

            foreach (var scenario in CreateDifferentialScenarios())
            {
                var planned = new SearchSchema().TryPlanSource(
                    scenario.SourceName,
                    CreateRequest(scenario));

                AssertExecutionPlanMirrorsResult(scenario, planned);

                var baseline = ApplyBaselineOperations(
                    scenario.Read(root, SourceExecutionPlan.Empty(scenario.Identity)),
                    scenario.FullPredicate,
                    scenario.OrderBy.Count > 0,
                    scenario.OrderDescending,
                    scenario.Skip,
                    scenario.Take);
                var actual = ApplyResidualOperations(
                    scenario.Read(root, planned.ExecutionPlan),
                    scenario.ResidualPredicate,
                    planned.ResidualOrderBy.Count > 0,
                    scenario.OrderDescending,
                    planned.ResidualSkip,
                    planned.ResidualTake);

                CollectionAssert.AreEqual(
                    baseline,
                    actual,
                    scenario.Name);
            }
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void Planner_ShouldMirrorEveryPublicProjectionColumnInExecutionPlan()
    {
        var root = CreateTemporaryRoot();
        try
        {
            CreateFixture(root);

            foreach (var source in CreateSourceDefinitions())
            {
                foreach (var column in source.PublicColumns)
                {
                    var result = new SearchSchema().TryPlanSource(
                        source.SourceName,
                        new SourcePlanRequest
                        {
                            Identity = source.Identity,
                            RequiredColumns =
                            [
                                new SourceColumnRef($"{source.Alias}.{column}")
                            ]
                        });

                    CollectionAssert.AreEqual(
                        new[] { column },
                        result.AcceptedColumns.Select(static value => value.Name).ToArray(),
                        $"{source.SourceName}.{column} result projection");
                    CollectionAssert.AreEqual(
                        new[] { column },
                        result.ExecutionPlan.AcceptedColumns
                            .Select(static value => value.Name)
                            .ToArray(),
                        $"{source.SourceName}.{column} execution projection");

                    var rows = source.Read(root, result.ExecutionPlan);
                    Assert.IsTrue(
                        rows.Count > 0,
                        $"{source.SourceName}.{column} did not produce a seeded row.");
                    _ = GetPublicValue(rows[0], column);
                }
            }
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void CompiledCrossSourceQuery_ShouldPreserveAcceptedFieldsAndResidualWindow()
    {
        var root = CreateTemporaryRoot();
        var escapedRoot = Escape(root);
        try
        {
            CreateFixture(root);

            var result = Compile(
                    $"select p.Path, p.Origin, p.EntryKind, " +
                    "m.PatternId, m.MatchIndex, m.ByteOffset, m.ByteLength, " +
                    "m.LineNumber, m.Utf16Column, m.Utf16Length " +
                    $"from search.paths('{escapedRoot}') p " +
                    $"inner join search.matches('{escapedRoot}', '{Literal}') m on p.Path = m.Path " +
                    "where p.EntryKind = 'file' and m.LineNumber >= 2 " +
                    "order by p.Path desc, m.MatchIndex desc take 2")
                .Run();

            Assert.AreEqual(2, result.Count);
            CollectionAssert.AreEqual(
                new[] { "nested/gamma.txt", "alpha.txt" },
                result.Rows.Select(static row => (string)row[0]!).ToArray());
            CollectionAssert.AreEqual(
                new long[] { 1, 1 },
                result.Rows.Select(static row => (long)row[4]).ToArray());
            Assert.IsTrue(result.Rows.All(row => row[1] is null));
            Assert.IsTrue(result.Rows.All(row => (string)row[2]! == "file"));
            Assert.IsTrue(result.Rows.All(row => row[3] is null));
            Assert.IsTrue(result.Rows.All(row => (long)row[7] == 2));
            CollectionAssert.AreEqual(
                new long[] { 0, 5 },
                result.Rows.Select(static row => (long)row[8]).ToArray());
            Assert.IsTrue(result.Rows.All(row => (long)row[9] == 4));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    private static IReadOnlyList<DifferentialScenario> CreateDifferentialScenarios()
    {
        var matchesAccepted = GreaterOrEqual("m.LineNumber", 2L);
        var matchesMixed = And(
            Equal("m.Path", "alpha.txt"),
            Equal("m.MatchText", Literal));
        var linesAccepted = GreaterOrEqual("l.OccurrenceCount", 2L);
        var linesMixed = And(
            Equal("l.Path", "alpha.txt"),
            Equal("l.LineText", "TODO TODO\n"));
        var filesAccepted = NotEqual("f.Path", "beta.txt");
        var countsAccepted = GreaterOrEqual("c.OccurrenceCount", 1L);
        var pathsAccepted = Equal("p.EntryKind", "file");
        var matchesResidual = Equal("m.MatchText", Literal);

        var matches = SourceColumns(
            "matches",
            "m",
            MatchColumns,
            ReadMatches);
        var lines = SourceColumns(
            "lines",
            "l",
            LineColumns,
            ReadLines);
        var files = SourceColumns(
            "files",
            "f",
            FileColumns,
            ReadFiles);
        var counts = SourceColumns(
            "counts",
            "c",
            CountColumns,
            ReadCounts);
        var paths = SourceColumns(
            "paths",
            "p",
            PathColumns,
            ReadPaths);

        return
        [
            new DifferentialScenario(
                "matches-accepted-predicate-and-slice",
                matches,
                matchesAccepted,
                Matches(matchesAccepted),
                ResidualPredicate: null,
                OrderBy: [],
                OrderDescending: false,
                Skip: 1,
                Take: 1),
            new DifferentialScenario(
                "matches-mixed-residual-predicate",
                matches,
                matchesMixed,
                row => ((SearchMatch)row).Path == "alpha.txt" &&
                       ((SearchMatch)row).MatchText == Literal,
                row => ((SearchMatch)row).MatchText == Literal,
                OrderBy: [],
                OrderDescending: false,
                Skip: 0,
                Take: 2),
            new DifferentialScenario(
                "matches-residual-order-and-slice",
                matches,
                matchesResidual,
                row => ((SearchMatch)row).MatchText == Literal,
                row => ((SearchMatch)row).MatchText == Literal,
                OrderBy: [Descending("m.Path")],
                OrderDescending: true,
                Skip: 1,
                Take: 3),
            new DifferentialScenario(
                "lines-accepted-predicate-and-slice",
                lines,
                linesAccepted,
                Matches(linesAccepted),
                ResidualPredicate: null,
                OrderBy: [],
                OrderDescending: false,
                Skip: 0,
                Take: 1),
            new DifferentialScenario(
                "lines-mixed-residual-predicate",
                lines,
                linesMixed,
                row => ((SearchLine)row).Path == "alpha.txt" &&
                       ((SearchLine)row).LineText == "TODO TODO\n",
                row => ((SearchLine)row).LineText == "TODO TODO\n",
                OrderBy: [],
                OrderDescending: false,
                Skip: 0,
                Take: 1),
            new DifferentialScenario(
                "files-accepted-predicate-and-slice",
                files,
                filesAccepted,
                Matches(filesAccepted),
                ResidualPredicate: null,
                OrderBy: [],
                OrderDescending: false,
                Skip: 1,
                Take: 1),
            new DifferentialScenario(
                "files-residual-order-and-slice",
                files,
                Predicate: null,
                FullPredicate: null,
                ResidualPredicate: null,
                OrderBy: [Descending("f.Path")],
                OrderDescending: true,
                Skip: 1,
                Take: 1),
            new DifferentialScenario(
                "counts-accepted-predicate-and-slice",
                counts,
                countsAccepted,
                Matches(countsAccepted),
                ResidualPredicate: null,
                OrderBy: [],
                OrderDescending: false,
                Skip: 1,
                Take: 1),
            new DifferentialScenario(
                "counts-residual-order-and-slice",
                counts,
                Predicate: null,
                FullPredicate: null,
                ResidualPredicate: null,
                OrderBy: [Descending("c.Path")],
                OrderDescending: true,
                Skip: 1,
                Take: 2),
            new DifferentialScenario(
                "paths-accepted-predicate-and-slice",
                paths,
                pathsAccepted,
                Matches(pathsAccepted),
                ResidualPredicate: null,
                OrderBy: [],
                OrderDescending: false,
                Skip: 1,
                Take: 2),
            new DifferentialScenario(
                "paths-residual-order-and-slice",
                paths,
                Predicate: null,
                FullPredicate: null,
                ResidualPredicate: null,
                OrderBy: [Descending("p.Path")],
                OrderDescending: true,
                Skip: 1,
                Take: 2)
        ];
    }

    private static IReadOnlyList<SourceDefinition> CreateSourceDefinitions()
    {
        return
        [
            SourceColumns(
                "matches",
                "m",
                MatchColumns,
                ReadMatches),
            SourceColumns(
                "lines",
                "l",
                LineColumns,
                ReadLines),
            SourceColumns(
                "files",
                "f",
                FileColumns,
                ReadFiles),
            SourceColumns(
                "counts",
                "c",
                CountColumns,
                ReadCounts),
            SourceColumns(
                "paths",
                "p",
                PathColumns,
                ReadPaths)
        ];
    }

    private static void AssertExecutionPlanMirrorsResult(
        DifferentialScenario scenario,
        SourcePlanResult result)
    {
        CollectionAssert.AreEqual(
            scenario.Source.PublicColumns.ToArray(),
            result.AcceptedColumns.Select(static column => column.Name).ToArray(),
            $"{scenario.Name} result projection");
        CollectionAssert.AreEqual(
            scenario.Source.PublicColumns.ToArray(),
            result.ExecutionPlan.AcceptedColumns
                .Select(static column => column.Name)
                .ToArray(),
            $"{scenario.Name} execution projection");
        Assert.AreSame(
            result.AcceptedPredicate,
            result.ExecutionPlan.AcceptedPredicate,
            scenario.Name);
        Assert.AreEqual(result.AcceptedSkip, result.ExecutionPlan.AcceptedSkip, scenario.Name);
        Assert.AreEqual(result.AcceptedTake, result.ExecutionPlan.AcceptedTake, scenario.Name);
        Assert.AreEqual(
            scenario.ResidualPredicate is not null,
            result.ResidualPredicate is not null,
            scenario.Name);

        var acceptsSlice = scenario.ResidualPredicate is null &&
                           scenario.OrderBy.Count == 0;
        Assert.AreEqual(
            acceptsSlice ? scenario.Skip : null,
            result.AcceptedSkip,
            scenario.Name);
        Assert.AreEqual(
            acceptsSlice ? scenario.Take : null,
            result.AcceptedTake,
            scenario.Name);
        Assert.AreEqual(
            acceptsSlice ? null : scenario.Skip,
            result.ResidualSkip,
            scenario.Name);
        Assert.AreEqual(
            acceptsSlice ? null : scenario.Take,
            result.ResidualTake,
            scenario.Name);

        if (scenario.OrderBy.Count > 0)
        {
            Assert.AreEqual(0, result.AcceptedOrderBy.Count, scenario.Name);
            Assert.AreEqual(scenario.OrderBy.Count, result.ResidualOrderBy.Count, scenario.Name);
            Assert.IsNull(result.AcceptedSkip, scenario.Name);
            Assert.IsNull(result.AcceptedTake, scenario.Name);
        }
    }

    private static SourcePlanRequest CreateRequest(DifferentialScenario scenario)
    {
        return new SourcePlanRequest
        {
            Identity = scenario.Source.Identity,
            RequiredColumns = scenario.Source.PublicColumns
                .Select(column => new SourceColumnRef($"{scenario.Source.Alias}.{column}"))
                .ToArray(),
            Predicate = scenario.Predicate,
            OrderBy = scenario.OrderBy,
            Skip = scenario.Skip,
            Take = scenario.Take
        };
    }

    private static string[] ApplyBaselineOperations(
        IReadOnlyList<object> rows,
        Func<object, bool>? predicate,
        bool hasOrder,
        bool descending,
        long? skip,
        long? take)
    {
        IEnumerable<object> query = rows;
        if (predicate is not null)
            query = query.Where(predicate);

        query = ApplyOrder(query, hasOrder, descending);
        query = ApplyWindow(query, skip, take);
        return query.Select(Signature).ToArray();
    }

    private static string[] ApplyResidualOperations(
        IReadOnlyList<object> rows,
        Func<object, bool>? residualPredicate,
        bool hasResidualOrder,
        bool descending,
        long? residualSkip,
        long? residualTake)
    {
        IEnumerable<object> query = rows;
        if (residualPredicate is not null)
            query = query.Where(residualPredicate);

        query = ApplyOrder(query, hasResidualOrder, descending);
        query = ApplyWindow(query, residualSkip, residualTake);
        return query.Select(Signature).ToArray();
    }

    private static IEnumerable<object> ApplyOrder(
        IEnumerable<object> rows,
        bool hasOrder,
        bool descending)
    {
        if (!hasOrder)
            return rows;

        return descending
            ? rows.OrderByDescending(GetPath, StringComparer.Ordinal)
            : rows.OrderBy(GetPath, StringComparer.Ordinal);
    }

    private static IEnumerable<object> ApplyWindow(
        IEnumerable<object> rows,
        long? skip,
        long? take)
    {
        if (skip.HasValue)
            rows = rows.Skip(checked((int)skip.Value));
        if (take.HasValue)
            rows = rows.Take(checked((int)take.Value));
        return rows;
    }

    private static string GetPath(object row)
    {
        return row switch
        {
            SearchMatch match => match.Path,
            SearchLine line => line.Path,
            SearchFile file => file.Path,
            SearchCount count => count.Path,
            SearchPath path => path.Path,
            _ => throw new InvalidOperationException($"Unknown Search row type '{row.GetType()}'.")
        };
    }

    private static string Signature(object row)
    {
        return row switch
        {
            SearchMatch match => string.Join(
                "|",
                "match",
                match.Path,
                match.PatternId,
                match.MatchIndex,
                match.ByteOffset,
                match.ByteLength,
                match.LineNumber,
                match.Utf16Column,
                match.Utf16Length,
                match.MatchText,
                string.Join(",", match.Captures.Select(static capture =>
                    $"{capture.GroupName}:{capture.GroupIndex}:{capture.CaptureIndex}:{capture.Success}:{capture.Text}")),
                string.Join(",", match.Context.Select(static context =>
                    $"{context.RelativeLine}:{context.LineNumber}:{context.LineText}"))),
            SearchLine line => string.Join(
                "|",
                "line",
                line.Path,
                line.Origin,
                line.PatternId,
                line.LineNumber,
                line.ByteOffset,
                line.LineText,
                line.OccurrenceCount),
            SearchFile file => string.Join(
                "|",
                "file",
                file.Path,
                file.Origin,
                file.PatternId),
            SearchCount count => string.Join(
                "|",
                "count",
                count.Path,
                count.Origin,
                count.PatternId,
                count.OccurrenceCount,
                count.MatchingLineCount,
                count.BytesScanned,
                count.Complete),
            SearchPath path => string.Join(
                "|",
                "path",
                path.Path,
                path.Origin,
                path.EntryKind),
            _ => throw new InvalidOperationException($"Unknown Search row type '{row.GetType()}'.")
        };
    }

    private static object? GetPublicValue(object row, string column)
    {
        return row switch
        {
            SearchMatch match => column switch
            {
                nameof(SearchMatch.Path) => match.Path,
                nameof(SearchMatch.PatternId) => match.PatternId,
                nameof(SearchMatch.MatchIndex) => match.MatchIndex,
                nameof(SearchMatch.ByteOffset) => match.ByteOffset,
                nameof(SearchMatch.ByteLength) => match.ByteLength,
                nameof(SearchMatch.LineNumber) => match.LineNumber,
                nameof(SearchMatch.Utf16Column) => match.Utf16Column,
                nameof(SearchMatch.Utf16Length) => match.Utf16Length,
                nameof(SearchMatch.MatchText) => match.MatchText,
                nameof(SearchMatch.Captures) => match.Captures,
                nameof(SearchMatch.Context) => match.Context,
                _ => throw UnknownColumn(column, row)
            },
            SearchLine line => column switch
            {
                nameof(SearchLine.Path) => line.Path,
                nameof(SearchLine.Origin) => line.Origin,
                nameof(SearchLine.PatternId) => line.PatternId,
                nameof(SearchLine.LineNumber) => line.LineNumber,
                nameof(SearchLine.ByteOffset) => line.ByteOffset,
                nameof(SearchLine.LineText) => line.LineText,
                nameof(SearchLine.OccurrenceCount) => line.OccurrenceCount,
                _ => throw UnknownColumn(column, row)
            },
            SearchFile file => column switch
            {
                nameof(SearchFile.Path) => file.Path,
                nameof(SearchFile.Origin) => file.Origin,
                nameof(SearchFile.PatternId) => file.PatternId,
                _ => throw UnknownColumn(column, row)
            },
            SearchCount count => column switch
            {
                nameof(SearchCount.Path) => count.Path,
                nameof(SearchCount.Origin) => count.Origin,
                nameof(SearchCount.PatternId) => count.PatternId,
                nameof(SearchCount.OccurrenceCount) => count.OccurrenceCount,
                nameof(SearchCount.MatchingLineCount) => count.MatchingLineCount,
                nameof(SearchCount.BytesScanned) => count.BytesScanned,
                nameof(SearchCount.Complete) => count.Complete,
                _ => throw UnknownColumn(column, row)
            },
            SearchPath path => column switch
            {
                nameof(SearchPath.Path) => path.Path,
                nameof(SearchPath.Origin) => path.Origin,
                nameof(SearchPath.EntryKind) => path.EntryKind,
                _ => throw UnknownColumn(column, row)
            },
            _ => throw new InvalidOperationException($"Unknown Search row type '{row.GetType()}'.")
        };
    }

    private static InvalidOperationException UnknownColumn(string column, object row)
    {
        return new InvalidOperationException(
            $"Column '{column}' is not public on Search row type '{row.GetType()}'.");
    }

    private static Func<object, bool> Matches(SourcePredicateExpression predicate)
    {
        return row => row switch
        {
            SearchMatch match => SearchPredicateEvaluator.Matches(
                predicate,
                match,
                SearchPredicateValues.GetMatchValue),
            SearchLine line => SearchPredicateEvaluator.Matches(
                predicate,
                line,
                SearchPredicateValues.GetLineValue),
            SearchFile file => SearchPredicateEvaluator.Matches(
                predicate,
                file,
                SearchPredicateValues.GetFileValue),
            SearchCount count => SearchPredicateEvaluator.Matches(
                predicate,
                count,
                SearchPredicateValues.GetCountValue),
            SearchPath path => SearchPredicateEvaluator.Matches(
                predicate,
                path,
                SearchPredicateValues.GetPathValue),
            _ => throw new InvalidOperationException($"Unknown Search row type '{row.GetType()}'.")
        };
    }

    private static SourcePredicateComparison Equal(string column, object? value)
    {
        return new SourcePredicateComparison(
            SourcePredicateComparisonOperator.Equal,
            new SourcePredicateColumn(new SourceColumnRef(column)),
            new SourcePredicateLiteral(value));
    }

    private static SourcePredicateComparison NotEqual(string column, object? value)
    {
        return new SourcePredicateComparison(
            SourcePredicateComparisonOperator.NotEqual,
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

    private static SourcePredicateLogical And(
        SourcePredicateExpression left,
        SourcePredicateExpression right)
    {
        return new SourcePredicateLogical(
            SourcePredicateLogicalOperator.And,
            left,
            right);
    }

    private static OrderByExpression Descending(string column)
    {
        return new OrderByExpression(
            new SourceColumnRef(column),
            OrderDirection.Descending);
    }

    private static SourceDefinition SourceColumns(
        string sourceName,
        string alias,
        IReadOnlyList<string> publicColumns,
        Func<string, SourceExecutionPlan, IReadOnlyList<object>> read)
    {
        return new SourceDefinition(
            sourceName,
            alias,
            publicColumns,
            new SourceIdentity("search", sourceName, "planner-differential", alias),
            read);
    }

    private static IReadOnlyList<object> ReadMatches(
        string root,
        SourceExecutionPlan plan)
    {
        return new SearchMatchesSource(
                SearchRequest.Create(root, Literal),
                CreateContext(plan, MatchColumns),
                readerFactory: static path => SearchTextReader.Open(path, SearchEncodingMode.Auto))
            .Chunks
            .SelectMany(static chunk => chunk)
            .Cast<object>()
            .ToArray();
    }

    private static IReadOnlyList<object> ReadLines(
        string root,
        SourceExecutionPlan plan)
    {
        return new SearchLinesSource(
                SearchRequest.Create(root, Literal),
                CreateContext(plan, LineColumns),
                readerFactory: static path => SearchTextReader.Open(path, SearchEncodingMode.Auto))
            .Chunks
            .SelectMany(static chunk => chunk)
            .Cast<object>()
            .ToArray();
    }

    private static IReadOnlyList<object> ReadFiles(
        string root,
        SourceExecutionPlan plan)
    {
        return new SearchFilesSource(
                SearchRequest.Create(root, Literal),
                CreateContext(plan, FileColumns),
                readerFactory: static path => SearchTextReader.Open(path, SearchEncodingMode.Auto))
            .Chunks
            .SelectMany(static chunk => chunk)
            .Cast<object>()
            .ToArray();
    }

    private static IReadOnlyList<object> ReadCounts(
        string root,
        SourceExecutionPlan plan)
    {
        return new SearchCountsSource(
                SearchRequest.Create(root, Literal),
                CreateContext(plan, CountColumns),
                readerFactory: static path => SearchTextReader.Open(path, SearchEncodingMode.Auto))
            .Chunks
            .SelectMany(static chunk => chunk)
            .Cast<object>()
            .ToArray();
    }

    private static IReadOnlyList<object> ReadPaths(
        string root,
        SourceExecutionPlan plan)
    {
        return new SearchPathsSource(
                root,
                CreateContext(plan, PathColumns))
            .Chunks
            .SelectMany(static chunk => chunk)
            .Cast<object>()
            .ToArray();
    }

    private static SourceExecutionContext CreateContext(
        SourceExecutionPlan plan,
        IReadOnlyList<string> columns)
    {
        return RuntimeV2TestContexts.CreateExecutionContext(
            allColumns: columns
                .Select((name, index) => (ISchemaColumn)new SchemaColumn(name, index, typeof(object)))
                .ToArray(),
            executionPlan: plan);
    }

    private static void CreateFixture(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "nested"));
        File.WriteAllText(
            Path.Combine(root, "alpha.txt"),
            "header\nTODO TODO\n");
        File.WriteAllText(
            Path.Combine(root, "beta.txt"),
            "TODO\n");
        File.WriteAllText(
            Path.Combine(root, "empty.txt"),
            "none\n");
        File.WriteAllText(
            Path.Combine(root, "nested", "gamma.txt"),
            "TODO\nTODO\n");
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
        return Path.Combine(Path.GetTempPath(), $"musoq-search-planner-{Guid.NewGuid():N}");
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

    private sealed record SourceDefinition(
        string SourceName,
        string Alias,
        IReadOnlyList<string> PublicColumns,
        SourceIdentity Identity,
        Func<string, SourceExecutionPlan, IReadOnlyList<object>> Read);

    private sealed record DifferentialScenario(
        string Name,
        SourceDefinition Source,
        SourcePredicateExpression? Predicate,
        Func<object, bool>? FullPredicate,
        Func<object, bool>? ResidualPredicate,
        IReadOnlyList<OrderByExpression> OrderBy,
        bool OrderDescending,
        long? Skip,
        long? Take)
    {
        public string SourceName => Source.SourceName;

        public SourceIdentity Identity => Source.Identity;

        public Func<string, SourceExecutionPlan, IReadOnlyList<object>> Read => Source.Read;
    }
}
