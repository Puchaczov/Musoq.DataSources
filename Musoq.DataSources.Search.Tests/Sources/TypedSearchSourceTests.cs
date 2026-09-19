#nullable enable

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;

using Musoq.DataSources.Search.Components.Diagnostics;
using Musoq.DataSources.Search.Tests.Infrastructure;
using Musoq.DataSources.Search.Sources;

namespace Musoq.DataSources.Search.Tests.Sources;

[TestClass]
public sealed class TypedSearchSourceTests
{
    [TestMethod]
    public void TypedMatches_ShouldUseExplicitInsensitiveCaseAndSourceMatchText()
    {
        var root = CreateFixture(("alpha.txt", "prefix TODO suffix\n"));
        try
        {
            var result = Compile(
                    $"select m.Path, m.MatchText from search.matches(" +
                    $"'{EscapeSql(root)}', 'todo', " +
                    "(Text: (CaseMode: 'insensitive'))) m")
                .Run();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("alpha.txt", result.Rows[0][0]);
            Assert.AreEqual("TODO", result.Rows[0][1]);
        }
        finally
        {
            DeleteFixture(root);
        }
    }

    [TestMethod]
    public void TypedOptions_ShouldKeepSimpleTextDefaultsAndRecursiveScope()
    {
        var root = CreateFixture(
            ("top.txt", "TODO\n"),
            ("nested/child.txt", "TODO\n"),
            ("nested/none.txt", "DONE\n"));
        try
        {
            var simple = Compile(
                    $"select m.Path from search.matches('{EscapeSql(root)}', 'TODO') m order by m.Path")
                .Run();
            var explicitDefaults = Compile(
                    $"select m.Path from search.matches('{EscapeSql(root)}', 'TODO', " +
                    "(Text: (CaseMode: 'sensitive', Encoding: 'auto', WholeWord: false), " +
                    "Scope: (Recursive: true))) m order by m.Path")
                .Run();

            CollectionAssert.AreEqual(
                simple.Rows.Select(row => (string)row[0]).ToArray(),
                explicitDefaults.Rows.Select(row => (string)row[0]).ToArray());
            CollectionAssert.AreEqual(
                new[] { "nested/child.txt", "top.txt" },
                simple.Rows.Select(row => (string)row[0]).ToArray());
        }
        finally
        {
            DeleteFixture(root);
        }
    }

    [TestMethod]
    public void TypedSinglePattern_ShouldUseRegexModeAndRecordOptions()
    {
        var root = CreateFixture(("alpha.txt", "before ISSUE-42 after\n"));
        try
        {
            var result = Compile(
                    $"select m.MatchText from search.matches('{EscapeSql(root)}', " +
                    "'ISSUE-([0-9]+)', (Text: (Mode: 'regex'), " +
                    "Records: (Mode: 'physical-line', MaxBytes: 1024))) m")
                .Run();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("ISSUE-42", result.Rows[0][0]);
        }
        finally
        {
            DeleteFixture(root);
        }
    }

    [TestMethod]
    public void TypedOptions_ShouldRejectRecordsForLiteralWithExactPath()
    {
        var exception = Assert.ThrowsException<SearchRequestException>(() =>
            new SearchMatchesTypedSource(
                "fixture",
                "TODO",
                new SearchMatchOptionsInput(records: new SearchRecordsInput()),
                RuntimeV2TestContexts.CreateExecutionContext()));

        Assert.AreEqual("records", exception.Diagnostic.Location?.ArgumentName);
    }

    [TestMethod]
    public void TypedOptions_ShouldRejectUnconfiguredGlobalRulesWithExactPath()
    {
        var exception = Assert.ThrowsException<SearchRequestException>(() =>
            new SearchMatchesTypedSource(
                "fixture",
                "TODO",
                new SearchMatchOptionsInput(
                    scope: new SearchScopeInput(globalIgnoreRules: new[] { "*.tmp" })),
                RuntimeV2TestContexts.CreateExecutionContext()));

        Assert.AreEqual("scope.globalIgnoreRules", exception.Diagnostic.Location?.ArgumentName);
    }

    [TestMethod]
    public void TypedOptions_ShouldRequireRulesForConfiguredGlobalIgnores()
    {
        var exception = Assert.ThrowsException<SearchRequestException>(() =>
            new SearchMatchesTypedSource(
                "fixture",
                "TODO",
                new SearchMatchOptionsInput(
                    scope: new SearchScopeInput(globalIgnores: "configured")),
                RuntimeV2TestContexts.CreateExecutionContext()));

        Assert.AreEqual("scope.globalIgnoreRules", exception.Diagnostic.Location?.ArgumentName);
    }

    [TestMethod]
    public void TypedCountsAndAudit_ShouldExposeExactNegativeEvidence()
    {
        var root = CreateFixture(
            ("match.txt", "TODO TODO\n"),
            ("none.txt", "DONE\n"));
        try
        {
            var counts = Compile(
                    $"select c.Path, c.OccurrenceCount, c.Complete from search.counts(" +
                    $"'{EscapeSql(root)}', 'TODO', (Scope: (Recursive: true))) c order by c.Path")
                .Run();
            var audit = Compile(
                    $"select a.Complete, a.CountsExact, a.ScopeExhausted, a.Occurrences " +
                    $"from search.audit('{EscapeSql(root)}', 'TODO', (Scope: (Recursive: true))) a")
                .Run();

            Assert.AreEqual(2, counts.Count);
            Assert.AreEqual("match.txt", counts.Rows[0][0]);
            Assert.AreEqual(2L, counts.Rows[0][1]);
            Assert.AreEqual(true, counts.Rows[0][2]);
            Assert.AreEqual("none.txt", counts.Rows[1][0]);
            Assert.AreEqual(0L, counts.Rows[1][1]);
            Assert.AreEqual(true, counts.Rows[1][2]);

            Assert.AreEqual(1, audit.Count);
            Assert.AreEqual(true, audit.Rows[0][0]);
            Assert.AreEqual(true, audit.Rows[0][1]);
            Assert.AreEqual(true, audit.Rows[0][2]);
            Assert.AreEqual(2L, audit.Rows[0][3]);
        }
        finally
        {
            DeleteFixture(root);
        }
    }

    [TestMethod]
    public void TypedMany_ShouldAcceptInlinePatternArray()
    {
        var root = CreateFixture(("alpha.txt", "TODO FIXME\n"));
        try
        {
            var result = Compile(
                    $"select m.PatternId, m.MatchText from search.many(" +
                    $"'{EscapeSql(root)}', array {{" +
                    "(Id: 'todo', Pattern: 'TODO')," +
                    "(Id: 'fixme', Pattern: 'FIXME')" +
                    "}) m order by m.PatternId")
                .Run();

            CollectionAssert.AreEqual(
                new[] { "fixme", "todo" },
                result.Rows.Select(row => (string)row[0]).ToArray());
        }
        finally
        {
            DeleteFixture(root);
        }
    }

    [TestMethod]
    public void TypedMany_ShouldScanLiteralAndRegexPatternsFromOneRequest()
    {
        var root = CreateFixture(("alpha.txt", "TODO ISSUE-42 TODO\n"));
        try
        {
            var result = Compile(
                    $"select m.PatternId, m.MatchText, m.LineNumber from search.many(" +
                    $"'{EscapeSql(root)}', array {{" +
                    "(Id: 'todo', Pattern: 'TODO')," +
                    "(Id: 'issue', Pattern: 'ISSUE-[0-9]+', Mode: 'regex')" +
                    "}) m order by m.PatternId, m.MatchIndex")
                .Run();

            Assert.AreEqual(3, result.Count);
            CollectionAssert.AreEqual(
                new[] { "ISSUE-42", "TODO", "TODO" },
                result.Rows.Select(row => (string)row[1]).ToArray());
        }
        finally
        {
            DeleteFixture(root);
        }
    }

    [TestMethod]
    public void TypedMany_ShouldPreserveSourceSpellingForInsensitiveLiterals()
    {
        var root = CreateFixture(("alpha.txt", "todo TODO ToDo\n"));
        try
        {
            var result = Compile(
                    $"select m.MatchIndex, m.MatchText from search.many(" +
                    $"'{EscapeSql(root)}', array {{ (Id: 'todo', Pattern: 'todo') }}, " +
                    "(Text: (CaseMode: 'insensitive'))) m order by m.MatchIndex")
                .Run();

            Assert.AreEqual(3, result.Count);
            CollectionAssert.AreEqual(
                new[] { "todo", "TODO", "ToDo" },
                result.Rows.Select(row => (string)row[1]).ToArray());
        }
        finally
        {
            DeleteFixture(root);
        }
    }

    [TestMethod]
    public void TypedMany_ShouldRetainBoundedContextForLiteralAndRegexRows()
    {
        var root = CreateFixture(("alpha.txt", "before\nTODO ISSUE-42\nafter\n"));
        try
        {
            var result = Compile(
                    $"select m.PatternId, m.MatchText, c.RelativeLine, c.LineText from search.many(" +
                    $"'{EscapeSql(root)}', array {{" +
                    "(Id: 'todo', Pattern: 'TODO')," +
                    "(Id: 'issue', Pattern: 'ISSUE-[0-9]+', Mode: 'regex')" +
                    "}, (Context: (BeforeLines: 1, AfterLines: 1))) m " +
                    "cross apply m.Context c order by m.PatternId, c.RelativeLine")
                .Run();

            Assert.AreEqual(4, result.Count);
            CollectionAssert.AreEqual(
                new[] { "-1", "1", "-1", "1" },
                result.Rows.Select(row => row[2].ToString()).ToArray());
            CollectionAssert.AreEqual(
                new[] { "before\n", "after\n", "before\n", "after\n" },
                result.Rows.Select(row => (string)row[3]).ToArray());
        }
        finally
        {
            DeleteFixture(root);
        }
    }

    [TestMethod]
    public void TypedBytes_ShouldUseHexPatternAndTypedWindow()
    {
        var root = CreateFixture(("alpha.bin", "xTODOy"));
        try
        {
            var result = Compile(
                    $"select b.Path, b.ByteOffset, b.WindowByteLength from search.bytes(" +
                    $"'{EscapeSql(root)}', '54 4f 44 4f', " +
                    "(Window: (BeforeBytes: 1, AfterBytes: 1))) b")
                .Run();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("alpha.bin", result.Rows[0][0]);
            Assert.AreEqual(1L, result.Rows[0][1]);
            Assert.AreEqual(6L, result.Rows[0][2]);
        }
        finally
        {
            DeleteFixture(root);
        }
    }

    [TestMethod]
    public void TypedScope_ShouldExcludeHiddenEntriesUnlessExplicitlyEnabled()
    {
        var root = CreateFixture(
            ("visible.txt", "TODO\n"),
            (".hidden.txt", "TODO\n"),
            (".hidden-dir/nested.txt", "TODO\n"));
        try
        {
            var defaultResult = Compile(
                    $"select m.Path from search.matches('{EscapeSql(root)}', 'TODO') m order by m.Path")
                .Run();
            var hiddenResult = Compile(
                    $"select m.Path from search.matches('{EscapeSql(root)}', 'TODO', " +
                    "(Scope: (HiddenEntries: true))) m order by m.Path")
                .Run();

            CollectionAssert.AreEqual(new[] { "visible.txt" },
                defaultResult.Rows.Select(row => (string)row[0]).ToArray());
            CollectionAssert.AreEqual(
                new[] { ".hidden-dir/nested.txt", ".hidden.txt", "visible.txt" },
                hiddenResult.Rows.Select(row => (string)row[0]).ToArray());
        }
        finally
        {
            DeleteFixture(root);
        }
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
        var root = Path.Combine(
            Path.GetTempPath(),
            $"musoq-search-typed-{Guid.NewGuid():N}");
        foreach (var file in files)
        {
            var path = Path.Combine(root, file.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, file.Content);
        }

        return root;
    }

    private static string EscapeSql(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("'", "''", StringComparison.Ordinal);
    }

    private static void DeleteFixture(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}
