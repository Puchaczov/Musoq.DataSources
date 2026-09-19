#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;

using Musoq.DataSources.Search.Sources;

using Musoq.DataSources.Search.Components.Contracts;

using Musoq.DataSources.Search.Components.Diagnostics;

using Musoq.DataSources.Search.Components.Many;

using Musoq.DataSources.Search.Components.Traversal;

using Musoq.DataSources.Search.Tests.Infrastructure;

namespace Musoq.DataSources.Search.Tests.Sources.Many;

[TestClass]
public sealed class SearchManySourceTests
{
    private const string TwoPatternCollection =
        "array { (Id: 'todo', Pattern: 'TODO'), (Id: 'fixme', Pattern: 'FIXME') }";

    private const string MissingPatternCollection =
        "array { (Id: 'todo', Pattern: 'TODO'), (Id: 'missing', Pattern: 'MISSING') }";

    [TestMethod]
    public void ManySource_ShouldPreservePatternIdentityAndPerPatternOrdinals()
    {
        var root = CreateFixture(
            ("alpha.txt", "TODO TODO\nFIXME"),
            ("beta.txt", "TODO\n"),
            ("delta.txt", "FIXME"),
            ("gamma.txt", "nothing\n"));

        try
        {
            var rows = new SearchManySource(
                    root,
                    new SearchManyRequest(
                    [
                        new SearchManyPattern("todo", "TODO", SearchPatternMode.Literal),
                        new SearchManyPattern("fixme", "FIXME", SearchPatternMode.Literal)
                    ]),
                    RuntimeV2TestContexts.CreateExecutionContext())
                .Chunks
                .SelectMany(static chunk => chunk)
                .OrderBy(static row => row.Path, StringComparer.Ordinal)
                .ThenBy(static row => row.PatternId, StringComparer.Ordinal)
                .ThenBy(static row => row.MatchIndex)
                .Select(static row => (row.Path, row.PatternId, row.MatchIndex, row.MatchText))
                .ToArray();

            CollectionAssert.AreEquivalent(
                new[]
                {
                    ("alpha.txt", (string?)"todo", 0L, (string?)"TODO"),
                    ("alpha.txt", (string?)"todo", 1L, (string?)"TODO"),
                    ("alpha.txt", (string?)"fixme", 0L, (string?)"FIXME"),
                    ("beta.txt", (string?)"todo", 0L, (string?)"TODO"),
                    ("delta.txt", (string?)"fixme", 0L, (string?)"FIXME")
                },
                rows);
        }
        finally
        {
            DeleteFixture(root);
        }
    }

    [TestMethod]
    public void CompiledManyQuery_ShouldExposeTypedLabeledOccurrences()
    {
        var root = CreateFixture(
            ("alpha.txt", "TODO TODO\nFIXME"),
            ("beta.txt", "TODO\n"),
            ("delta.txt", "FIXME"),
            ("gamma.txt", "nothing\n"));

        try
        {
            var escapedRoot = EscapeSql(root);
            var result = Compile(
                    $"select Path, PatternId, MatchIndex from search.many('{escapedRoot}', {TwoPatternCollection}) " +
                    "order by Path, PatternId, MatchIndex")
                .Run();

            CollectionAssert.AreEqual(
                new[] { "Path", "PatternId", "MatchIndex" },
                result.Columns.Select(column => column.ColumnName).ToArray());
            Assert.AreEqual(5, result.Count);
            CollectionAssert.AreEqual(
                new[]
                {
                    ("alpha.txt", "fixme", 0L),
                    ("alpha.txt", "todo", 0L),
                    ("alpha.txt", "todo", 1L),
                    ("beta.txt", "todo", 0L),
                    ("delta.txt", "fixme", 0L)
                },
                result.Rows.Select(row => ((string)row[0], (string)row[1], (long)row[2])).ToArray());
        }
        finally
        {
            DeleteFixture(root);
        }
    }

    [TestMethod]
    public void CompiledSql_ShouldProduceTypedPerPatternCountsAndAnyHits()
    {
        var root = CreateFixture(
            ("alpha.txt", "TODO TODO\nFIXME"),
            ("beta.txt", "TODO\n"),
            ("delta.txt", "FIXME"),
            ("gamma.txt", "nothing\n"));

        try
        {
            var escapedRoot = EscapeSql(root);
            var result = Compile(
                    $"select m.Path as Path, m.PatternId as PatternId, Count(m.MatchIndex) as OccurrenceCount " +
                    $"from search.many('{escapedRoot}', {TwoPatternCollection}) m " +
                    "group by m.Path, m.PatternId order by m.Path, m.PatternId")
                .Run();
            var any = Compile(
                    $"select m.Path as Path, true as AnyHit " +
                    $"from search.many('{escapedRoot}', {TwoPatternCollection}) m " +
                    "group by m.Path order by m.Path")
                .Run();

            CollectionAssert.AreEqual(
                new[] { "Path", "PatternId", "OccurrenceCount" },
                result.Columns.Select(column => column.ColumnName).ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    ("alpha.txt", "fixme", 1L),
                    ("alpha.txt", "todo", 2L),
                    ("beta.txt", "todo", 1L),
                    ("delta.txt", "fixme", 1L)
                },
                result.Rows.Select(row => ((string)row[0], (string)row[1], (long)row[2])).ToArray());
            CollectionAssert.AreEqual(
                new[] { "Path", "AnyHit" },
                any.Columns.Select(column => column.ColumnName).ToArray());
            CollectionAssert.AreEqual(
                new[] { ("alpha.txt", true), ("beta.txt", true), ("delta.txt", true) },
                any.Rows.Select(row => ((string)row[0], (bool)row[1])).ToArray());
        }
        finally
        {
            DeleteFixture(root);
        }
    }

    [TestMethod]
    public void CompiledSql_ShouldExpressAllAndAtLeastKPatternsWithDistinctLabels()
    {
        var root = CreateFixture(
            ("alpha.txt", "TODO TODO\nFIXME"),
            ("beta.txt", "TODO\n"),
            ("delta.txt", "FIXME"),
            ("gamma.txt", "nothing\n"));

        try
        {
            var escapedRoot = EscapeSql(root);
            var all = Compile(
                    $"select m.Path, Count(distinct m.PatternId) as PatternCount " +
                    $"from search.many('{escapedRoot}', {TwoPatternCollection}) m " +
                    "group by m.Path having Count(distinct m.PatternId) = 2 order by m.Path")
                .Run();
            var atLeastTwo = Compile(
                    $"select m.Path, Count(distinct m.PatternId) as PatternCount " +
                    $"from search.many('{escapedRoot}', {TwoPatternCollection}) m " +
                    "group by m.Path having Count(distinct m.PatternId) >= 2 order by m.Path")
                .Run();

            Assert.AreEqual(1, all.Count);
            Assert.AreEqual("alpha.txt", all[0][0]);
            Assert.AreEqual(2L, all[0][1]);
            Assert.AreEqual(1, atLeastTwo.Count);
            Assert.AreEqual("alpha.txt", atLeastTwo[0][0]);
            Assert.AreEqual(2L, atLeastTwo[0][1]);
        }
        finally
        {
            DeleteFixture(root);
        }
    }

    [TestMethod]
    public void CompiledSql_ShouldExpressNoneAndMissingPatternWithoutFalseNegatives()
    {
        var root = CreateFixture(
            ("alpha.txt", "TODO TODO\nFIXME"),
            ("beta.txt", "TODO\n"),
            ("delta.txt", "FIXME"),
            ("gamma.txt", "nothing\n"));

        try
        {
            var escapedRoot = EscapeSql(root);
            var none = Compile(
                    $"select p.Path from search.paths('{escapedRoot}') p " +
                    $"left outer join search.many('{escapedRoot}', {TwoPatternCollection}) m on p.Path = m.Path " +
                    "where m.Path is null order by p.Path")
                .Run();
            var allWithMissing = Compile(
                    $"select m.Path from search.many('{escapedRoot}', {MissingPatternCollection}) m " +
                    "group by m.Path having Count(distinct m.PatternId) = 2 order by m.Path")
                .Run();

            Assert.AreEqual(1, none.Count);
            Assert.AreEqual("gamma.txt", none[0][0]);
            Assert.AreEqual(0, allWithMissing.Count);
        }
        finally
        {
            DeleteFixture(root);
        }
    }

    [TestMethod]
    public void CompleteNegativeSummary_ShouldReadEveryEligibleFileBeforeReturning()
    {
        var root = CreateFixture(
            ("alpha.txt", "TODO"),
            ("beta.txt", "FIXME"),
            ("gamma.txt", "nothing"));
        var opened = new List<string>();
        var counters = new SearchScopeCounters();

        try
        {
            var rows = new SearchManySource(
                    root,
                    new SearchManyRequest(
                    [
                        new SearchManyPattern("todo", "TODO", SearchPatternMode.Literal),
                        new SearchManyPattern("fixme", "FIXME", SearchPatternMode.Literal)
                    ]),
                    RuntimeV2TestContexts.CreateExecutionContext(),
                    path =>
                    {
                        opened.Add(Path.GetFileName(path));
                        return new StreamReader(path);
                    },
                    counters)
                .Chunks
                .SelectMany(static chunk => chunk)
                .ToArray();

            Assert.AreEqual(2, rows.Length);
            CollectionAssert.AreEquivalent(
                new[] { "alpha.txt", "beta.txt", "gamma.txt" },
                opened);
            Assert.AreEqual(3L, counters.FilesConsidered);
            Assert.AreEqual(3L, counters.CandidatesYielded);
            Assert.AreEqual(3L, counters.ContentOpenAttempts);
        }
        finally
        {
            DeleteFixture(root);
        }
    }

    [TestMethod]
    public void ManySource_ShouldScanRegexWithoutReopeningContent()
    {
        var root = CreateFixture(("alpha.txt", "TODO\n"));
        var request = new SearchManyRequest(
        [
            new SearchManyPattern("regex", "TODO|FIXME", SearchPatternMode.Regex)
        ]);
        try
        {
            var rows = new SearchManySource(
                    root,
                    request,
                    RuntimeV2TestContexts.CreateExecutionContext(),
                    _ => new StringReader("TODO\n"))
                .Chunks
                .SelectMany(static chunk => chunk)
                .ToArray();

            Assert.AreEqual(1, rows.Length);
            Assert.AreEqual("regex", rows[0].PatternId);
            Assert.AreEqual("TODO", rows[0].MatchText);
        }
        finally
        {
            DeleteFixture(root);
        }
    }

    [TestMethod]
    public void ManySource_ShouldRejectIncompleteExecutionOptionsBeforeOpeningContent()
    {
        var request = new SearchManyRequest(
            [new SearchManyPattern("todo", "TODO", SearchPatternMode.Literal)],
            new SearchManyOptions(take: 1));
        var readerOpened = false;
        var root = Path.Combine(Path.GetTempPath(), $"musoq-search-many-options-{Guid.NewGuid():N}");

        var exception = Assert.ThrowsException<SearchRequestException>(
            () => new SearchManySource(
                    root,
                    request,
                    RuntimeV2TestContexts.CreateExecutionContext(),
                    _ =>
                    {
                        readerOpened = true;
                        return new StringReader(string.Empty);
                    })
                .Chunks
                .ToArray());

        StringAssert.Contains(exception.Diagnostic.Explanation, "later completion surface");
        Assert.IsFalse(readerOpened);
    }

    private static CompiledQuery Compile(string query)
    {
        return InstanceCreatorHelpers.CompileForExecution(
            query,
            Guid.NewGuid().ToString(),
            new SearchSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    private static string CreateFixture(params (string RelativePath, string Content)[] files)
    {
        var root = Path.Combine(Path.GetTempPath(), $"musoq-search-many-source-{Guid.NewGuid():N}");
        foreach (var file in files)
        {
            var path = Path.Combine(root, file.RelativePath);
            var parent = Path.GetDirectoryName(path);
            if (parent is not null)
                Directory.CreateDirectory(parent);

            File.WriteAllText(path, file.Content);
        }

        return root;
    }

    private static string EscapeSql(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal);
    }

    private static void DeleteFixture(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}
