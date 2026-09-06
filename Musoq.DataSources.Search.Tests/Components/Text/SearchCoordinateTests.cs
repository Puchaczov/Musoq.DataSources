#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;

using Musoq.DataSources.Search.Entities;

using Musoq.DataSources.Search.Sources;

using Musoq.DataSources.Search.Components.Contracts;

using Musoq.DataSources.Search.Components.Text;

namespace Musoq.DataSources.Search.Tests.Components.Text;

[TestClass]
public sealed class SearchCoordinateTests
{
    [TestMethod]
    public void Utf8Coordinates_ShouldTrackOriginalBytesUtf16ColumnsAndLineStarts()
    {
        var root = CreateTemporaryRoot();
        var path = Path.Combine(root, "unicode.txt");
        const string firstLine = "😀e\u0301\tTODO\r\n";
        const string secondLine = "Zażółć TODO";
        var content = firstLine + secondLine;

        try
        {
            Directory.CreateDirectory(root);
            WriteUtf8(path, content, includeBom: false);

            var request = SearchRequest.Create(root, "TODO");
            var matches = ReadMatches(request);
            var lines = ReadLines(request);
            var bytes = File.ReadAllBytes(path);

            Assert.AreEqual(2, matches.Length);
            Assert.AreEqual(1L, matches[0].LineNumber);
            Assert.AreEqual(5L, matches[0].Utf16Column);
            Assert.AreEqual(Encoding.UTF8.GetByteCount("😀e\u0301\t"), matches[0].ByteOffset);
            Assert.AreEqual(4L, matches[0].ByteLength);
            Assert.AreEqual(4L, matches[0].Utf16Length);
            Assert.AreEqual(2L, matches[1].LineNumber);
            Assert.AreEqual(7L, matches[1].Utf16Column);
            Assert.AreEqual(
                Encoding.UTF8.GetByteCount(firstLine + "Zażółć "),
                matches[1].ByteOffset);
            Assert.AreEqual(4L, matches[1].ByteLength);
            Assert.AreEqual(4L, matches[1].Utf16Length);

            foreach (var match in matches)
            {
                Assert.IsTrue(match.ByteOffset.HasValue);
                Assert.IsTrue(match.ByteLength.HasValue);
                var offset = checked((int)match.ByteOffset.Value);
                var length = checked((int)match.ByteLength.Value);
                Assert.AreEqual(
                    match.MatchText,
                    Encoding.UTF8.GetString(bytes, offset, length));
            }

            Assert.AreEqual(2, lines.Length);
            Assert.AreEqual(0L, lines[0].ByteOffset);
            Assert.AreEqual(Encoding.UTF8.GetByteCount(firstLine), lines[1].ByteOffset);
            CollectionAssert.AreEqual(
                new[] { firstLine, secondLine },
                lines.Select(static line => line.LineText).ToArray());
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void Utf16BomCoordinates_ShouldIncludePreambleInOriginalOffsets()
    {
        var root = CreateTemporaryRoot();
        var path = Path.Combine(root, "utf16.txt");
        const string content = "prefix\nTODO";
        var encoding = new UnicodeEncoding(
            bigEndian: false,
            byteOrderMark: true,
            throwOnInvalidBytes: true);

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllBytes(
                path,
                encoding.GetPreamble().Concat(encoding.GetBytes(content)).ToArray());

            var matches = ReadMatches(SearchRequest.Create(
                path,
                "TODO",
                encoding: "utf16-le-bom"));

            Assert.AreEqual(1, matches.Length);
            Assert.AreEqual(2L, matches[0].LineNumber);
            Assert.AreEqual(0L, matches[0].Utf16Column);
            Assert.AreEqual(
                encoding.GetPreamble().Length + encoding.GetByteCount("prefix\n"),
                matches[0].ByteOffset);
            Assert.AreEqual(8L, matches[0].ByteLength);
            Assert.AreEqual(4L, matches[0].Utf16Length);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void CrLfSplitAcrossReaderBlocks_ShouldKeepLineAndMatchOffsets()
    {
        var root = CreateTemporaryRoot();
        var path = Path.Combine(root, "split.txt");
        var prefix = new string('x', SearchCharBuffer.RequestedLength - 1);
        var content = prefix + "\r\nTODO";

        try
        {
            Directory.CreateDirectory(root);
            WriteUtf8(path, content, includeBom: false);

            var request = SearchRequest.Create(root, "TODO");
            var matches = ReadMatches(request);
            var lines = ReadLines(request);
            var expectedOffset = Encoding.UTF8.GetByteCount(prefix + "\r\n");

            Assert.AreEqual(1, matches.Length);
            Assert.AreEqual(2L, matches[0].LineNumber);
            Assert.AreEqual(0L, matches[0].Utf16Column);
            Assert.AreEqual(expectedOffset, matches[0].ByteOffset);
            Assert.AreEqual(4L, matches[0].ByteLength);

            Assert.AreEqual(1, lines.Length);
            Assert.AreEqual(2L, lines[0].LineNumber);
            Assert.AreEqual(expectedOffset, lines[0].ByteOffset);
            Assert.AreEqual("TODO", lines[0].LineText);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void LoneCarriageReturn_ShouldRemainContentWithinOnePhysicalLine()
    {
        var root = CreateTemporaryRoot();
        var path = Path.Combine(root, "lone-cr.txt");
        const string content = "TODO\rnext TODO";

        try
        {
            Directory.CreateDirectory(root);
            WriteUtf8(path, content, includeBom: false);

            var request = SearchRequest.Create(root, "TODO");
            var matches = ReadMatches(request);
            var lines = ReadLines(request);

            Assert.AreEqual(2, matches.Length);
            Assert.IsTrue(matches.All(static match => match.LineNumber == 1));
            Assert.AreEqual(0L, matches[0].Utf16Column);
            Assert.AreEqual(10L, matches[1].Utf16Column);
            Assert.AreEqual(0L, matches[0].ByteOffset);
            Assert.AreEqual(10L, matches[1].ByteOffset);
            Assert.AreEqual(1, lines.Length);
            Assert.AreEqual(1L, lines[0].LineNumber);
            Assert.AreEqual(0L, lines[0].ByteOffset);
            Assert.AreEqual(content, lines[0].LineText);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void NoFinalNewline_ShouldMapTheFinalLineStartAtEof()
    {
        var root = CreateTemporaryRoot();
        var path = Path.Combine(root, "eof.txt");
        const string content = "prefix\nTODO";

        try
        {
            Directory.CreateDirectory(root);
            WriteUtf8(path, content, includeBom: false);

            var request = SearchRequest.Create(root, "TODO");
            var matches = ReadMatches(request);
            var lines = ReadLines(request);

            Assert.AreEqual(1, matches.Length);
            Assert.AreEqual(2L, matches[0].LineNumber);
            Assert.AreEqual(0L, matches[0].Utf16Column);
            Assert.AreEqual(7L, matches[0].ByteOffset);
            Assert.AreEqual(4L, matches[0].ByteLength);
            Assert.AreEqual(1, lines.Length);
            Assert.AreEqual(7L, lines[0].ByteOffset);
            Assert.AreEqual("TODO", lines[0].LineText);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void InjectedDecodedReader_ShouldNotInventOriginalByteCoordinates()
    {
        var root = CreateTemporaryRoot();
        var path = Path.Combine(root, "injected.txt");

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(path, "ignored", Encoding.UTF8);

            var context = RuntimeV2TestContexts.CreateExecutionContext();
            var matches = new SearchMatchesSource(
                    path,
                    "TODO",
                    context,
                    _ => new StringReader("TODO"))
                .Chunks
                .SelectMany(static chunk => chunk)
                .ToArray();
            var lines = new SearchLinesSource(
                    path,
                    "TODO",
                    context,
                    _ => new StringReader("TODO"))
                .Chunks
                .SelectMany(static chunk => chunk)
                .ToArray();

            Assert.AreEqual(1, matches.Length);
            Assert.IsNull(matches[0].ByteOffset);
            Assert.IsNull(matches[0].ByteLength);
            Assert.AreEqual(1, lines.Length);
            Assert.IsNull(lines[0].ByteOffset);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void CoordinateStorage_ShouldRetainChecked64BitOffsets()
    {
        var coordinate = new SearchCharCoordinate(
            IsMapped: true,
            ByteOffset: long.MaxValue - 3,
            ByteLength: 3,
            CanStart: true,
            CanEnd: true);
        var span = new MatchSpan(
            long.MaxValue - 4,
            4,
            long.MaxValue - 3,
            long.MaxValue - 2);
        var match = new SearchMatch(
            "large.txt",
            0,
            long.MaxValue - 4,
            4,
            long.MaxValue - 3,
            long.MaxValue - 2,
            4,
            "TODO");

        Assert.AreEqual(long.MaxValue, coordinate.ByteEndExclusive);
        Assert.AreEqual(long.MaxValue, span.EndExclusive);
        Assert.AreEqual(long.MaxValue - 4, match.ByteOffset);
        Assert.AreEqual(4L, match.ByteLength);
        Assert.AreEqual(long.MaxValue - 3, match.LineNumber);
        Assert.AreEqual(long.MaxValue - 2, match.Utf16Column);
        Assert.AreEqual(4L, match.Utf16Length);
    }

    private static SearchMatch[] ReadMatches(SearchRequest request)
    {
        return new SearchMatchesSource(
                request,
                RuntimeV2TestContexts.CreateExecutionContext())
            .Chunks
            .SelectMany(static chunk => chunk)
            .ToArray();
    }

    private static SearchLine[] ReadLines(SearchRequest request)
    {
        return new SearchLinesSource(
                request,
                RuntimeV2TestContexts.CreateExecutionContext())
            .Chunks
            .SelectMany(static chunk => chunk)
            .ToArray();
    }

    private static void WriteUtf8(string path, string content, bool includeBom)
    {
        var encoding = new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: includeBom,
            throwOnInvalidBytes: true);
        var bytes = (includeBom ? encoding.GetPreamble() : Array.Empty<byte>())
            .Concat(encoding.GetBytes(content))
            .ToArray();
        File.WriteAllBytes(path, bytes);
    }

    private static string CreateTemporaryRoot()
    {
        return Path.Combine(Path.GetTempPath(), $"musoq-search-coordinate-{Guid.NewGuid():N}");
    }

    private static void DeleteTemporaryRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}
