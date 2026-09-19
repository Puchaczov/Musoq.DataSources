#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Search;
using Musoq.DataSources.Search.ContractTests.Infrastructure;
using Musoq.DataSources.Search.Sources;
using Musoq.DataSources.Search.Testing;
using Musoq.DataSources.Tests.Common;

namespace Musoq.DataSources.Search.ContractTests.Sources;

[TestClass]
public sealed class SearchTypedBytesContractTests : SearchContractTestBase
{
    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void Bytes_ShouldReturnRawBoundaryOffsetsAndIndependentIndexes()
    {
        WithExtendedCorpus(fixture =>
        {
            var result = Sql.ExecuteForRoot(
                fixture.PathFor("extended/bytes"),
                root => $"select b.Path, b.MatchIndex, b.ByteOffset, b.ByteLength " +
                        $"from search.bytes('{root}', '54 4f 44 4f') b " +
                        "order by b.Path, b.MatchIndex");

            Assert.AreEqual(4, result.Count);
            CollectionAssert.AreEqual(
                new[] { "boundary.bin", "end.bin", "middle.bin", "start.bin" },
                result.Rows.Select(row => (string)row[0]!).ToArray());
            CollectionAssert.AreEqual(
                new long[] { 8_191, 1, 2, 0 },
                result.Rows.Select(row => (long)row[2]!).ToArray());
            Assert.IsTrue(result.Rows.All(row => (long)row[3]! == 4));

            var direct = new SearchBytesTypedSource(
                    fixture.PathFor("extended/bytes"),
                    "54 4f 44 4f",
                    RuntimeV2TestContexts.CreateExecutionContext())
                .Chunks
                .SelectMany(chunk => chunk)
                .OrderBy(row => row.Path, StringComparer.Ordinal)
                .ThenBy(row => row.MatchIndex)
                .ToArray();
            Assert.IsTrue(direct.All(row =>
                row.MatchedBytes is not null &&
                Convert.ToHexString(row.MatchedBytes) == "544F444F"));
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void Bytes_ShouldApplyTypedMasksAndClipWindowsAtFileBoundaries()
    {
        WithCorpus(ContractCorpusProfile.Bytes, fixture =>
        {
            var masked = Sql.ExecuteForRoot(
                fixture.PathFor("bytes/payload.bin"),
                root => $"select b.ByteOffset from search.bytes('{root}', '54 4f 00 4f', " +
                        "(MaskHex: 'ff ff 00 ff')) b order by b.ByteOffset");
            CollectionAssert.AreEqual(new long[] { 2, 8 }, masked.Rows.Select(row => (long)row[0]!).ToArray());

            var window = Sql.ExecuteForRoot(
                fixture.PathFor("bytes/payload.bin"),
                root => $"select b.ByteOffset, b.WindowStartByteOffset, b.WindowByteLength, " +
                        $"b.WindowComplete from search.bytes('{root}', '54 4f 44 4f', " +
                        "(Window: (BeforeBytes: 3, AfterBytes: 3))) b where b.MatchIndex = 0");
            Assert.AreEqual(1, window.Count);
            Assert.AreEqual(2L, window.Value<long>(0, 0));
            Assert.AreEqual(0L, window.Value<long>(0, 1));
            Assert.AreEqual(9L, window.Value<long>(0, 2));
            Assert.IsFalse(window.Value<bool>(0, 3));

            var direct = new SearchBytesTypedSource(
                    fixture.PathFor("bytes/payload.bin"),
                    "54 4f 44 4f",
                    new SearchBytesOptionsInput(
                        window: new SearchBytesWindowInput(beforeBytes: 3, afterBytes: 3)),
                    RuntimeV2TestContexts.CreateExecutionContext())
                .Chunks
                .SelectMany(chunk => chunk)
                .Single(row => row.MatchIndex == 0);
            CollectionAssert.AreEqual(
                new byte[] { 0x00, 0x11, 0x54, 0x4F, 0x44, 0x4F, 0x22, 0x33, 0x54 },
                direct.WindowBytes);
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void Bytes_ShouldRemainIndependentFromTextEncodingAndCoordinates()
    {
        WithCorpus(ContractCorpusProfile.Bytes, fixture =>
        {
            var result = Sql.ExecuteForRoot(
                fixture.PathFor("bytes/payload.bin"),
                root => $"select b.LineNumber, b.Utf16Column, b.MatchText " +
                        $"from search.bytes('{root}', '54 4f 44 4f') b");

            Assert.IsTrue(result.Rows.All(row => row[0] is null));
            Assert.IsTrue(result.Rows.All(row => row[1] is null));
            Assert.IsTrue(result.Rows.All(row => row[2] is null));

            var direct = new SearchBytesTypedSource(
                    fixture.PathFor("bytes/payload.bin"),
                    "54 4f 44 4f",
                    RuntimeV2TestContexts.CreateExecutionContext())
                .Chunks
                .SelectMany(chunk => chunk)
                .ToArray();
            Assert.IsTrue(direct.All(row => row.Captures.Count == 0 && row.Context.Count == 0));
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void ByteOracle_ShouldAgreeWithRawPayloadProjection()
    {
        WithCorpus(ContractCorpusProfile.Bytes, fixture =>
        {
            var payload = fixture.Files.Single(file => file.RelativePath == "bytes/payload.bin");
            var expected = SearchByteOracle.Scan(
                payload.Bytes,
                [0x54, 0x4F, 0x44, 0x4F],
                [0xFF, 0xFF, 0xFF, 0xFF]);
            var actual = Sql.ExecuteForRoot(
                    fixture.PathFor("bytes/payload.bin"),
                    root => $"select b.ByteOffset from search.bytes('{root}', '54 4f 44 4f') b " +
                            "order by b.ByteOffset")
                .Rows;

            CollectionAssert.AreEqual(
                expected.Select(match => match.Offset).ToArray(),
                actual.Select(row => (long)row[0]!).ToArray());

            var direct = new SearchBytesTypedSource(
                    fixture.PathFor("bytes/payload.bin"),
                    "54 4f 44 4f",
                    RuntimeV2TestContexts.CreateExecutionContext())
                .Chunks
                .SelectMany(chunk => chunk)
                .OrderBy(row => row.ByteOffset)
                .ToArray();
            CollectionAssert.AreEqual(
                expected.Select(match => Convert.ToHexString(match.Bytes)).ToArray(),
                direct.Select(row => Convert.ToHexString(row.MatchedBytes!)).ToArray());
        });
    }
}
