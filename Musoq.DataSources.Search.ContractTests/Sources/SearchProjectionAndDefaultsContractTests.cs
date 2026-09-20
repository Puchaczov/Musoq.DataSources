#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Search.ContractTests.Infrastructure;
using Musoq.DataSources.Search.Testing;

namespace Musoq.DataSources.Search.ContractTests.Sources;

[TestClass]
public sealed class SearchProjectionAndDefaultsContractTests : SearchContractTestBase
{
    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void LinesAndCounts_ShouldExposeTheirCompleteSourceSpecificUnits()
    {
        WithCorpus(ContractCorpusProfile.Minimal, fixture =>
        {
            var lines = Sql.ExecuteForRoot(
                fixture.Root,
                root => $"select l.Path, l.LineNumber, l.ByteOffset, l.LineText, l.OccurrenceCount " +
                        $"from search.lines('{root}', 'TODO') l " +
                        "order by l.Path, l.LineNumber");
            var counts = Sql.ExecuteForRoot(
                fixture.Root,
                root => $"select c.Path, c.OccurrenceCount, c.MatchingLineCount, c.BytesScanned, c.Complete " +
                        $"from search.counts('{root}', 'TODO') c order by c.Path");
            var files = Sql.ExecuteForRoot(
                fixture.Root,
                root => $"select f.Path, f.PatternId from search.files('{root}', 'TODO') f order by f.Path");

            Assert.AreEqual(6, lines.Count);
            var topLine = lines.Rows.Single(row => (string)row[0]! == "top.txt");
            Assert.AreEqual(1L, (long)topLine[1]!);
            Assert.AreEqual(0L, (long)topLine[2]!);
            Assert.AreEqual("TODO TODO\n", (string)topLine[3]!);
            Assert.AreEqual(2L, (long)topLine[4]!);

            Assert.AreEqual(8, counts.Count);
            var countByPath = counts.Rows.ToDictionary(row => (string)row[0]!);
            Assert.AreEqual(0L, countByPath["empty.txt"][1]);
            Assert.AreEqual(0L, countByPath["none.txt"][1]);
            Assert.AreEqual(2L, countByPath["top.txt"][1]);
            Assert.AreEqual(1L, countByPath["top.txt"][2]);
            Assert.AreEqual(10L, countByPath["top.txt"][3]);
            Assert.IsTrue(countByPath.Values.All(row => (bool)row[4]!));

            Assert.AreEqual(4, files.Count);
            Assert.IsTrue(files.Rows.All(row => row[1] is null));
            CollectionAssert.AreEqual(
                new[] { "nested/child.txt", "nested/crlf.txt", "nested/unicode.txt", "top.txt" },
                files.Rows.Select(row => (string)row[0]!).ToArray());
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void EmptyAndExplicitDefaultOptionRecords_ShouldPreserveSimpleMeaning()
    {
        WithCorpus(ContractCorpusProfile.Minimal, fixture =>
        {
            var simpleMatches = Sql.ExecuteForRoot(
                fixture.Root,
                root => $"select m.Path, m.MatchIndex, m.MatchText from search.matches('{root}', 'TODO') m " +
                        "order by m.Path, m.MatchIndex");
            var explicitMatches = Sql.ExecuteForRoot(
                fixture.Root,
                root => $"select m.Path, m.MatchIndex, m.MatchText from search.matches('{root}', 'TODO', " +
                        "(Text: (Mode: 'literal', CaseMode: 'sensitive', Encoding: 'auto', WholeWord: false), " +
                        "Scope: (Recursive: true, RepositoryIgnores: 'respect', " +
                        "GlobalIgnores: 'disabled', HiddenEntries: false, FollowLinks: false))) m " +
                        "order by m.Path, m.MatchIndex");

            Assert.AreEqual(
                ResultSignature.ForColumns(simpleMatches, 0, 1, 2),
                ResultSignature.ForColumns(explicitMatches, 0, 1, 2));

            var simplePaths = Sql.ExecuteForRoot(
                fixture.Root,
                root => $"select p.Path from search.paths('{root}') p order by p.Path");
            var explicitPaths = Sql.ExecuteForRoot(
                fixture.Root,
                root => $"select p.Path from search.paths('{root}', " +
                        "(Scope: (Recursive: true, RepositoryIgnores: 'respect', GlobalIgnores: 'disabled', " +
                        "HiddenEntries: false, FollowLinks: false), MaxFiles: 2147483647)) p " +
                        "order by p.Path");

            Assert.AreEqual(
                ResultSignature.ForColumns(simplePaths, 0),
                ResultSignature.ForColumns(explicitPaths, 0));
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void Context_ShouldAddBoundedNeighborRowsWithoutChangingOccurrences()
    {
        WithCorpus(ContractCorpusProfile.Minimal, fixture =>
        {
            var result = Sql.ExecuteForRoot(
                fixture.PathFor("nested/child.txt"),
                root => $"select m.MatchIndex, c.RelativeLine, c.LineNumber, c.LineText " +
                        $"from search.matches('{root}', 'TODO', " +
                        "(Context: (BeforeLines: 1, AfterLines: 1, MaxBytes: 1024))) m " +
                        "cross apply m.Context c order by c.RelativeLine");

            Assert.AreEqual(2, result.Count);
            CollectionAssert.AreEqual(
                new[] { -1, 1 },
                result.Rows.Select(row => (int)row[1]!).ToArray());
            CollectionAssert.AreEqual(
                new[] { 1L, 3L },
                result.Rows.Select(row => (long)row[2]!).ToArray());
            CollectionAssert.AreEqual(
                new[] { "before\n", "after\n" },
                result.Rows.Select(row => (string)row[3]!).ToArray());
            Assert.IsTrue(result.Rows.All(row => (long)row[0]! == 0));
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void Audit_ShouldEmitOneFreshTerminalRowIncludingZeroHitScans()
    {
        WithCorpus(ContractCorpusProfile.Minimal, fixture =>
        {
            var first = Sql.ExecuteForRoot(
                fixture.Root,
                root => $"select a.ScanId, a.Outcome, a.TerminalReason, a.Complete, " +
                        "a.ScopeExhausted, a.CountsExact, a.EligibleFiles, a.FilesCompleted, a.Occurrences " +
                        $"from search.audit('{root}', 'NEVER') a");
            var second = Sql.ExecuteForRoot(
                fixture.Root,
                root => $"select a.ScanId, a.Outcome, a.TerminalReason, a.Complete, " +
                        "a.ScopeExhausted, a.CountsExact, a.EligibleFiles, a.FilesCompleted, a.Occurrences " +
                        $"from search.audit('{root}', 'NEVER') a");

            Assert.AreEqual(1, first.Count);
            Assert.AreEqual(1, second.Count);
            Assert.AreNotEqual(first.Value<string>(0, 0), second.Value<string>(0, 0));
            Assert.IsFalse(string.IsNullOrWhiteSpace(first.Value<string>(0, 0)));
            Assert.AreEqual(1, first.Value<int>(0, 1));
            Assert.AreEqual("no-match", first.Value<string>(0, 2));
            Assert.IsTrue(first.Value<bool>(0, 3));
            Assert.IsTrue(first.Value<bool>(0, 4));
            Assert.IsTrue(first.Value<bool>(0, 5));
            Assert.AreEqual(8L, first.Value<long>(0, 6));
            Assert.AreEqual(8L, first.Value<long>(0, 7));
            Assert.AreEqual(0L, first.Value<long>(0, 8));
        });
    }
}
