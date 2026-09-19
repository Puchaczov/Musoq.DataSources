#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Search.ContractTests.Infrastructure;
using Musoq.DataSources.Search.Entities;
using Musoq.DataSources.Search.Sources;
using Musoq.DataSources.Search.Testing;
using Musoq.DataSources.Tests.Common;

namespace Musoq.DataSources.Search.ContractTests.Sources;

[TestClass]
public sealed class SearchManyAndBytesContractTests : SearchContractTestBase
{
    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void Many_ShouldAcceptMixedTypedPatternsInOneCompiledQuery()
    {
        WithCorpus(ContractCorpusProfile.All, fixture =>
        {
            var result = Sql.ExecuteForRoot(
                fixture.PathFor("regex"),
                root => $"select m.Path, m.PatternId, m.MatchIndex, m.MatchText " +
                        $"from search.many('{root}', array {{ " +
                        "(Id: 'todo', Pattern: 'TODO'), " +
                        "(Id: 'issue', Pattern: 'ISSUE-[0-9]+', Mode: 'regex') }) m " +
                        "order by m.PatternId, m.MatchIndex");

            Assert.AreEqual(4, result.Count);
            CollectionAssert.AreEqual(
                new[] { "issue", "issue", "todo", "todo" },
                result.Rows.Select(row => (string)row[1]!).ToArray());
            CollectionAssert.AreEqual(
                new[] { "ISSUE-42", "ISSUE-7", "TODO", "TODO" },
                result.Rows.Select(row => (string)row[3]!).ToArray());
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void Many_ShouldKeepEqualTextUnderIndependentPatternIds()
    {
        WithCorpus(ContractCorpusProfile.Minimal, fixture =>
        {
            var result = Sql.ExecuteForRoot(
                fixture.Root,
                root => $"select m.Path, m.PatternId, m.MatchIndex from search.many('{root}', array {{ " +
                        "(Id: 'primary', Pattern: 'TODO'), " +
                        "(Id: 'secondary', Pattern: 'TODO') }) m " +
                        "order by m.PatternId, m.Path, m.MatchIndex");

            Assert.AreEqual(14, result.Count);
            Assert.AreEqual(7, result.Rows.Count(row => (string)row[1]! == "primary"));
            Assert.AreEqual(7, result.Rows.Count(row => (string)row[1]! == "secondary"));
            var primary = result.Rows
                .Where(row => (string)row[1]! == "primary")
                .Select(row => string.Join("|", row[0], row[2]))
                .ToArray();
            var secondary = result.Rows
                .Where(row => (string)row[1]! == "secondary")
                .Select(row => string.Join("|", row[0], row[2]))
                .ToArray();
            CollectionAssert.AreEqual(primary, secondary);
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void Many_ShouldAcceptAExistingCoreCteCollection()
    {
        WithCorpus(ContractCorpusProfile.Minimal, fixture =>
        {
            var result = Sql.ExecuteForRoot(
                fixture.Root,
                root => $"with patterns as (" +
                        $"select 'todo' as Id, 'TODO' as Pattern, 'literal' as Mode " +
                        $"from search.paths('{root}') p take 1) " +
                        $"select m.Path, m.PatternId from search.many('{root}', patterns) m " +
                        "order by m.Path");

            Assert.AreEqual(7, result.Count);
            Assert.IsTrue(result.Rows.All(row => (string)row[1]! == "todo"));
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void Many_HostCollection_ShouldUseASeparateMatcherState()
    {
        WithCorpus(ContractCorpusProfile.Minimal, fixture =>
        {
            IReadOnlyList<SearchPatternInput> patterns =
            [
                new SearchPatternInput("todo", "TODO"),
                new SearchPatternInput("done", "DONE")
            ];

            var source = new SearchManyTypedSource(
                fixture.Root,
                patterns,
                RuntimeV2TestContexts.CreateExecutionContext());
            var rows = source.Chunks.SelectMany(chunk => chunk).ToArray();

            Assert.AreEqual(10, rows.Length);
            Assert.AreEqual(7, rows.Count(row => row.PatternId == "todo"));
            Assert.AreEqual(3, rows.Count(row => row.PatternId == "done"));
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void TypedBytes_ShouldExposeRawOffsetsAndWindows()
    {
        WithCorpus(ContractCorpusProfile.Bytes, fixture =>
        {
            var bytesRoot = fixture.PathFor("bytes");
            var result = Sql.ExecuteForRoot(
                bytesRoot,
                root => $"select b.Path, b.MatchIndex, b.ByteOffset, b.ByteLength, " +
                        "b.WindowStartByteOffset, b.WindowByteLength, b.WindowComplete " +
                        $"from search.bytes('{root}', '54 4f 44 4f', " +
                        "(Window: (BeforeBytes: 1, AfterBytes: 1))) b " +
                        "order by b.Path, b.MatchIndex");

            Assert.AreEqual(3, result.Count);
            CollectionAssert.AreEqual(
                new[] { "boundary.bin", "payload.bin", "payload.bin" },
                result.Rows.Select(row => (string)row[0]!).ToArray());
            CollectionAssert.AreEqual(
                new long[] { 8_191, 2, 8 },
                result.Rows.Select(row => (long)row[2]!).ToArray());
            Assert.IsTrue(result.Rows.All(row => (long)row[3]! == 4));
            Assert.IsTrue(result.Rows.All(row => (bool)row[6]!));

            var direct = new SearchBytesTypedSource(
                    bytesRoot,
                    "54 4f 44 4f",
                    new SearchBytesOptionsInput(
                        window: new SearchBytesWindowInput(beforeBytes: 1, afterBytes: 1)),
                    RuntimeV2TestContexts.CreateExecutionContext())
                .Chunks
                .SelectMany(chunk => chunk)
                .OrderBy(row => row.Path, StringComparer.Ordinal)
                .ThenBy(row => row.MatchIndex)
                .ToArray();
            Assert.AreEqual(3, direct.Length);
            Assert.IsTrue(direct.All(row => row.MatchedBytes is not null));
            Assert.IsTrue(direct.All(row => row.MatchedBytes!.SequenceEqual(
                new byte[] { 0x54, 0x4f, 0x44, 0x4f })));
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void TypedBytes_ShouldSupportWildcardNibbles()
    {
        WithCorpus(ContractCorpusProfile.Bytes, fixture =>
        {
            var result = Sql.ExecuteForRoot(
                fixture.PathFor("bytes/payload.bin"),
                root => $"select b.ByteOffset, b.ByteLength from search.bytes('{root}', '54 4f ?? 4f') b " +
                        "order by b.ByteOffset");

            CollectionAssert.AreEqual(
                new long[] { 2, 8 },
                result.Rows.Select(row => (long)row[0]!).ToArray());
            Assert.IsTrue(result.Rows.All(row => (long)row[1]! == 4));
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void ByteOracle_ShouldAgreeWithTheGeneratedPayloadFacts()
    {
        WithCorpus(ContractCorpusProfile.Bytes, fixture =>
        {
            var payload = fixture.Files.Single(file => file.RelativePath == "bytes/payload.bin");
            var expected = SearchByteOracle.Scan(
                payload.Bytes,
                [0x54, 0x4f, 0x44, 0x4f],
                [0xff, 0xff, 0xff, 0xff]);

            CollectionAssert.AreEqual(
                new long[] { 2, 8 },
                expected.Select(match => match.Offset).ToArray());
            CollectionAssert.AreEqual(
                new byte[] { 0x54, 0x4f, 0x44, 0x4f },
                expected[0].Bytes);
        });
    }
}
