#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.DataSources.Search.Sources;

using Musoq.DataSources.Search.Components.Bytes;

using Musoq.DataSources.Search.Tests.Infrastructure;

namespace Musoq.DataSources.Search.Tests.Sources.Bytes;

[TestClass]
public sealed class SearchByteSourceTests
{
    [TestMethod]
    public void Scanner_ShouldFindEveryByteValueWithIndependentOffsets()
    {
        var root = CreateTemporaryRoot();
        var path = Path.Combine(root, "all-bytes.bin");

        try
        {
            Directory.CreateDirectory(root);
            var payload = Enumerable.Range(0, 256)
                .Select(static value => (byte)value)
                .ToArray();
            File.WriteAllBytes(path, payload);

            for (var value = 0; value < 256; value++)
            {
                var pattern = new SearchBytePattern([(byte)value], [0xFF]);
                var matches = Scan(path, pattern, blockSize: 7);

                Assert.AreEqual(1, matches.Count, $"byte 0x{value:X2}");
                Assert.AreEqual(value, matches[0].Offset, $"byte 0x{value:X2}");
                CollectionAssert.AreEqual(
                    new[] { (byte)value },
                    matches[0].Bytes,
                    $"byte 0x{value:X2}");
            }
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void Scanner_ShouldMatchUnalignedEmbeddedNulSignaturesAcrossBlocks()
    {
        var root = CreateTemporaryRoot();
        var path = Path.Combine(root, "unaligned.bin");

        try
        {
            Directory.CreateDirectory(root);
            var payload = new byte[] { 0x11, 0x02, 0x00, 0x03, 0x04, 0x02, 0x00, 0x03, 0x04, 0x99 };
            File.WriteAllBytes(path, payload);
            var pattern = SearchBytePatternParser.ParseHex("02 00 03 04", mask: null);

            var matches = Scan(path, pattern, blockSize: 3);
            var expected = Oracle(payload, pattern);

            CollectionAssert.AreEqual(expected, matches.Select(static match => match.Offset).ToArray());
            Assert.AreEqual(2, matches.Count);
            CollectionAssert.AreEqual(
                new byte[] { 0x02, 0x00, 0x03, 0x04 },
                matches[0].Bytes);
            CollectionAssert.AreEqual(
                new byte[] { 0x02, 0x00, 0x03, 0x04 },
                matches[1].Bytes);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void Scanner_ShouldUseLeftmostNonOverlappingSelectionForWildcardMasks()
    {
        var root = CreateTemporaryRoot();
        var path = Path.Combine(root, "overlap.bin");

        try
        {
            Directory.CreateDirectory(root);
            var payload = new byte[] { 0xA1, 0xB2, 0xC3, 0xD4, 0xE5, 0xF6, 0x07 };
            File.WriteAllBytes(path, payload);
            var pattern = SearchBytePatternParser.ParseHex("?? ?? ??", mask: null);

            var matches = Scan(path, pattern, blockSize: 4);

            CollectionAssert.AreEqual(
                new long[] { 0, 3 },
                matches.Select(static match => match.Offset).ToArray());
            Assert.AreEqual(2, matches.Count);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void Scanner_ShouldClipShortFileWindowsAndMarkBoundaryTruncation()
    {
        var root = CreateTemporaryRoot();
        var path = Path.Combine(root, "short.bin");

        try
        {
            Directory.CreateDirectory(root);
            var payload = new byte[] { 0xA0, 0xB1, 0xC2, 0xB1, 0xC2 };
            File.WriteAllBytes(path, payload);
            var pattern = SearchBytePatternParser.ParseHex("b1 c2", null, 2, 2);

            var matches = ScanWindows(path, pattern, blockSize: 3);

            Assert.AreEqual(2, matches.Count);
            Assert.AreEqual(1L, matches[0].Offset);
            var firstWindow = matches[0].Window!;
            Assert.AreEqual(0L, firstWindow.StartByteOffset);
            Assert.AreEqual(5L, firstWindow.ByteLength);
            Assert.IsFalse(firstWindow.Complete);
            CollectionAssert.AreEqual(payload, firstWindow.Bytes!);

            Assert.AreEqual(3L, matches[1].Offset);
            var secondWindow = matches[1].Window!;
            Assert.AreEqual(1L, secondWindow.StartByteOffset);
            Assert.AreEqual(4L, secondWindow.ByteLength);
            Assert.IsFalse(secondWindow.Complete);
            CollectionAssert.AreEqual(
                new byte[] { 0xB1, 0xC2, 0xB1, 0xC2 },
                secondWindow.Bytes!);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void Scanner_ShouldReuseOverlappingWindowsAndRetainLargeBoundedWindows()
    {
        var root = CreateTemporaryRoot();
        var overlapPath = Path.Combine(root, "overlap.bin");
        var largePath = Path.Combine(root, "large.bin");

        try
        {
            Directory.CreateDirectory(root);
            var overlapPayload = Enumerable.Range(0, 80)
                .Select(static value => (byte)value)
                .ToArray();
            overlapPayload[20] = 0xAB;
            overlapPayload[21] = 0xCD;
            overlapPayload[23] = 0xAB;
            overlapPayload[24] = 0xCD;
            File.WriteAllBytes(overlapPath, overlapPayload);
            var overlapPattern = SearchBytePatternParser.ParseHex("ab cd", null, 4, 4);

            var overlapMatches = ScanWindows(overlapPath, overlapPattern, blockSize: 5);

            CollectionAssert.AreEqual(
                new long[] { 20, 23 },
                overlapMatches.Select(static match => match.Offset).ToArray());
            CollectionAssert.AreEqual(
                new byte[] { 16, 17, 18, 19, 0xAB, 0xCD, 22, 0xAB, 0xCD, 25 },
                overlapMatches[0].Window!.Bytes!);
            CollectionAssert.AreEqual(
                new byte[] { 19, 0xAB, 0xCD, 22, 0xAB, 0xCD, 25, 26, 27, 28 },
                overlapMatches[1].Window!.Bytes!);
            Assert.IsTrue(overlapMatches.All(static match => match.Window!.Complete));

            var largePayload = new byte[200_000];
            largePayload[100_000] = 0x7A;
            File.WriteAllBytes(largePath, largePayload);
            var largePattern = SearchBytePatternParser.ParseHex("7a", null, 60000, 60000);

            var largeMatches = ScanWindows(largePath, largePattern, blockSize: 4096);

            Assert.AreEqual(1, largeMatches.Count);
            var largeWindow = largeMatches[0].Window!;
            Assert.AreEqual(40_000L, largeWindow.StartByteOffset);
            Assert.AreEqual(120_001L, largeWindow.ByteLength);
            Assert.IsTrue(largeWindow.Complete);
            Assert.AreEqual(120_001, largeWindow.Bytes!.Length);
            Assert.AreEqual(0x7A, largeWindow.Bytes[60_000]);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void SourceAndCompiledQuery_ShouldExposeRawCoordinatesWithoutTextInference()
    {
        var root = CreateTemporaryRoot();
        var path = Path.Combine(root, "payload.bin");
        const string patternHex = "00 ff";

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllBytes(path, [0x10, 0x00, 0xFF, 0x20, 0x00, 0xFF]);

            var directSource = new SearchBytesSource(
                    root,
                    SearchBytePatternParser.ParseHex(patternHex, mask: null),
                    RuntimeV2TestContexts.CreateExecutionContext());
            var directRows = directSource
                .Chunks
                .SelectMany(static chunk => chunk)
                .ToArray();

            Assert.AreEqual(2, directRows.Length);
            CollectionAssert.AreEqual(new long[] { 1, 4 }, directRows.Select(static row => row.ByteOffset).ToArray());
            Assert.IsTrue(directRows.All(static row => row.ByteLength == 2));
            Assert.IsTrue(directRows.All(static row => row.LineNumber is null));
            Assert.IsTrue(directRows.All(static row => row.Utf16Column is null));
            Assert.IsTrue(directRows.All(static row => row.MatchText is null));
            Assert.IsTrue(directRows.All(static row => row.Captures.Count == 0));
            Assert.IsTrue(directRows.All(static row => row.Context.Count == 0));
            CollectionAssert.AreEqual(new byte[] { 0x00, 0xFF }, directRows[0].MatchedBytes);
            Assert.AreEqual(2L, directSource.LastExecution!.Occurrences);

            var escapedRoot = root.Replace("\\", "\\\\", StringComparison.Ordinal);
            var result = InstanceCreatorHelpers.CompileForExecution(
                    $"select Path, MatchIndex, ByteOffset, ByteLength " +
                    $"from search.bytes('{escapedRoot}', '{patternHex}') b " +
                    "order by ByteOffset",
                    Guid.NewGuid().ToString(),
                    new SearchSchemaProvider(),
                    EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables())
                .Run();

            CollectionAssert.AreEqual(
                new[] { "Path", "MatchIndex", "ByteOffset", "ByteLength" },
                result.Columns.Select(static column => column.ColumnName).ToArray());
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("payload.bin", result[0][0]);
            Assert.AreEqual(0L, result[0][1]);
            Assert.AreEqual(1L, result[0][2]);
            Assert.AreEqual(2L, result[0][3]);
            Assert.AreEqual(1L, result[1][1]);
            Assert.AreEqual(4L, result[1][2]);

        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void SourceAndCompiledQuery_ShouldExposeBoundedWindowCoordinates()
    {
        var root = CreateTemporaryRoot();
        var path = Path.Combine(root, "payload.bin");
        const string patternHex = "00 ff";

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllBytes(path, [0x10, 0x00, 0xFF, 0x20, 0x00, 0xFF]);

            var directSource = new SearchBytesSource(
                root,
                SearchBytePatternParser.ParseHex(patternHex, null, 1, 2),
                RuntimeV2TestContexts.CreateExecutionContext());
            var directRows = directSource.Chunks.SelectMany(static chunk => chunk).ToArray();

            Assert.AreEqual(2, directRows.Length);
            Assert.AreEqual(0L, directRows[0].WindowStartByteOffset);
            Assert.AreEqual(5L, directRows[0].WindowByteLength);
            Assert.AreEqual(true, directRows[0].WindowComplete);
            CollectionAssert.AreEqual(
                new byte[] { 0x10, 0x00, 0xFF, 0x20, 0x00 },
                directRows[0].WindowBytes);
            Assert.AreEqual(3L, directRows[1].WindowStartByteOffset);
            Assert.AreEqual(3L, directRows[1].WindowByteLength);
            Assert.AreEqual(false, directRows[1].WindowComplete);
            CollectionAssert.AreEqual(
                new byte[] { 0x20, 0x00, 0xFF },
                directRows[1].WindowBytes);

            var escapedRoot = root.Replace("\\", "\\\\", StringComparison.Ordinal);
            var result = InstanceCreatorHelpers.CompileForExecution(
                    $"select ByteOffset, WindowStartByteOffset, WindowByteLength, WindowComplete " +
                    $"from search.bytes('{escapedRoot}', '{patternHex}', " +
                    "(Window: (BeforeBytes: 1, AfterBytes: 2))) b " +
                    "order by ByteOffset",
                    Guid.NewGuid().ToString(),
                    new SearchSchemaProvider(),
                    EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables())
                .Run();

            CollectionAssert.AreEqual(
                new[] { "ByteOffset", "WindowStartByteOffset", "WindowByteLength", "WindowComplete" },
                result.Columns.Select(static column => column.ColumnName).ToArray());
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(1L, result[0][0]);
            Assert.AreEqual(0L, result[0][1]);
            Assert.AreEqual(5L, result[0][2]);
            Assert.AreEqual(true, result[0][3]);
            Assert.AreEqual(4L, result[1][0]);
            Assert.AreEqual(3L, result[1][1]);
            Assert.AreEqual(3L, result[1][2]);
            Assert.AreEqual(false, result[1][3]);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    private static List<(long Offset, byte[]? Bytes)> Scan(
        string path,
        SearchBytePattern pattern,
        int blockSize)
    {
        var matches = new List<(long Offset, byte[]? Bytes)>();
        SearchByteScanner.ScanFile(
            path,
            pattern,
            materializeMatchedBytes: true,
            (offset, bytes) => matches.Add((offset, bytes)),
            readerOpened: null,
            CancellationToken.None,
            blockSize);
        return matches;
    }

    private static List<(long Offset, byte[]? Bytes, SearchByteWindow? Window)> ScanWindows(
        string path,
        SearchBytePattern pattern,
        int blockSize)
    {
        var matches = new List<(long Offset, byte[]? Bytes, SearchByteWindow? Window)>();
        SearchByteScanner.ScanFile(
            path,
            pattern,
            materializeMatchedBytes: true,
            materializeWindowBytes: true,
            (offset, bytes, window) => matches.Add((offset, bytes, window)),
            readerOpened: null,
            CancellationToken.None,
            blockSize);
        return matches;
    }

    private static long[] Oracle(byte[] payload, SearchBytePattern pattern)
    {
        var expected = new List<long>();
        var bytes = pattern.Bytes.Span;
        var masks = pattern.Masks.Span;
        var nextAllowed = 0;
        for (var offset = 0; offset <= payload.Length - pattern.Length; offset++)
        {
            if (offset < nextAllowed)
                continue;

            var matches = true;
            for (var index = 0; index < pattern.Length; index++)
            {
                if ((payload[offset + index] & masks[index]) !=
                    (bytes[index] & masks[index]))
                {
                    matches = false;
                    break;
                }
            }

            if (!matches)
                continue;

            expected.Add(offset);
            nextAllowed = checked(offset + pattern.Length);
        }

        return expected.ToArray();
    }

    private static string CreateTemporaryRoot()
    {
        return Path.Combine(Path.GetTempPath(), $"musoq-search-bytes-{Guid.NewGuid():N}");
    }

    private static void DeleteTemporaryRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}
