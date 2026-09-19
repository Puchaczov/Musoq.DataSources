#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Search;
using Musoq.DataSources.Search.Components.Diagnostics;
using Musoq.DataSources.Search.ContractTests.Infrastructure;
using Musoq.DataSources.Search.Sources;
using Musoq.DataSources.Search.Testing;
using Musoq.DataSources.Tests.Common;
using Musoq.Converter.Exceptions;
using Musoq.Evaluator.Exceptions;

namespace Musoq.DataSources.Search.ContractTests.Sources;

[TestClass]
public sealed class SearchTypedManyContractTests : SearchContractTestBase
{
    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void Many_ShouldProjectCapturesAndContextFromTypedPatterns()
    {
        WithCorpus(ContractCorpusProfile.Regex, fixture =>
        {
            var result = Sql.ExecuteForRoot(
                fixture.PathFor("regex/captures.txt"),
                root => $"select m.PatternId, m.MatchIndex, c.GroupName, c.Success, c.Text " +
                        $"from search.many('{root}', array {{ " +
                        "(Id: 'capture', Pattern: 'name=(?<name>[a-z]+) id=(?<id>[0-9]+)', Mode: 'regex') }) m " +
                        "cross apply m.Captures c order by m.MatchIndex, c.GroupIndex");

            Assert.AreEqual(4, result.Count);
            Assert.IsTrue(result.Rows.All(row => (string)row[0]! == "capture"));
            CollectionAssert.AreEqual(
                new long[] { 0, 0, 1, 1 },
                result.Rows.Select(row => (long)row[1]!).ToArray());
            CollectionAssert.AreEqual(
                new[] { "name", "id", "name", "id" },
                result.Rows.Select(row => (string)row[2]!).ToArray());
            Assert.IsTrue(result.Rows.All(row => (bool)row[3]!));
            CollectionAssert.AreEqual(
                new[] { "alpha", "42", "beta", "7" },
                result.Rows.Select(row => (string)row[4]!).ToArray());
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void Many_ShouldKeepIndexesIndependentAndRetainInsensitiveSourceSpelling()
    {
        WithCorpus(ContractCorpusProfile.Text, fixture =>
        {
            var result = Sql.ExecuteForRoot(
                fixture.PathFor("text/case.txt"),
                root => $"select m.PatternId, m.MatchIndex, m.MatchText from search.many('{root}', array {{ " +
                        "(Id: 'primary', Pattern: 'todo'), (Id: 'secondary', Pattern: 'TODO') }, " +
                        "(Text: (CaseMode: 'insensitive'))) m " +
                        "order by m.PatternId, m.MatchIndex");

            Assert.AreEqual(12, result.Count);
            var primary = result.Rows
                .Where(row => (string)row[0]! == "primary")
                .Select(row => ((long)row[1]!, (string)row[2]!))
                .ToArray();
            var secondary = result.Rows
                .Where(row => (string)row[0]! == "secondary")
                .Select(row => ((long)row[1]!, (string)row[2]!))
                .ToArray();
            CollectionAssert.AreEqual(new long[] { 0, 1, 2, 3, 4, 5 }, primary.Select(value => value.Item1).ToArray());
            CollectionAssert.AreEqual(new long[] { 0, 1, 2, 3, 4, 5 }, secondary.Select(value => value.Item1).ToArray());
            CollectionAssert.AreEqual(
                new[] { "todo", "TODO", "ToDo", "TODO", "TODO", "TODO" },
                primary.Select(value => value.Item2).ToArray());
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void Many_HostAndCompiledSql_ShouldHaveEquivalentSemanticRows()
    {
        WithCorpus(ContractCorpusProfile.Minimal, fixture =>
        {
            IReadOnlyList<SearchPatternInput> patterns =
            [
                new SearchPatternInput("todo", "TODO"),
                new SearchPatternInput("done", "DONE")
            ];
            var host = SearchSqlHarness.MaterializeTyped(
                    new SearchManyTypedSource(
                        fixture.Root,
                        patterns,
                        RuntimeV2TestContexts.CreateExecutionContext()).Chunks)
                .Select(row => string.Join(
                    "|",
                    row.Path,
                    row.PatternId,
                    row.MatchIndex,
                    row.ByteOffset,
                    row.LineNumber,
                    row.Utf16Column,
                    row.MatchText))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            var sql = Sql.ExecuteForRoot(
                fixture.Root,
                root => $"select m.Path, m.PatternId, m.MatchIndex, m.ByteOffset, m.LineNumber, " +
                        $"m.Utf16Column, m.MatchText from search.many('{root}', array {{ " +
                        "(Id: 'todo', Pattern: 'TODO'), (Id: 'done', Pattern: 'DONE') }) m")
                .Rows
                .Select(row => string.Join("|", row.Select(value => value?.ToString() ?? "<null>")))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            CollectionAssert.AreEqual(host, sql);
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void TypedMany_ShouldRejectDuplicateIdsAndUnsupportedModes()
    {
        var root = Path.Combine(Path.GetTempPath(), $"musoq-search-many-invalid-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "one.txt"), "TODO\n");
            Assert.ThrowsException<SearchRequestException>(() =>
                new SearchManyTypedSource(
                    root,
                    [
                        new SearchPatternInput("same", "TODO"),
                        new SearchPatternInput("same", "DONE")
                    ],
                    RuntimeV2TestContexts.CreateExecutionContext())
                .Chunks
                .ToArray());

            Assert.ThrowsException<QueryExecutionException>(() =>
                Sql.ExecuteForRoot(
                    root,
                    escaped => $"select m.Path from search.many('{escaped}', array {{ " +
                                "(Id: 'bad', Pattern: 'TODO', Mode: 'glob') }) m"));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void RemovedJsonShapes_ShouldNotBindToManyOrBytes()
    {
        var root = Path.Combine(Path.GetTempPath(), $"musoq-search-typed-only-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllBytes(Path.Combine(root, "one.bin"), [0x54, 0x4F]);
            Assert.ThrowsException<MusoqQueryException>(() =>
                Sql.ExecuteForRoot(
                    root,
                    escaped => $"select m.Path from search.many('{escaped}', " +
                                "'{\"version\":1,\"patterns\":[]}') m"));
            Assert.ThrowsException<QueryExecutionException>(() =>
                Sql.ExecuteForRoot(
                    root,
                    escaped => $"select b.Path from search.bytes('{escaped}', " +
                                "'{\"version\":1,\"bytes\":\"54\"}') b"));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
