#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;

using Musoq.DataSources.Search.Sources;

using Musoq.DataSources.Search.Components.Contracts;

using Musoq.DataSources.Search.Components.Text;

using Musoq.DataSources.Search.Components.Bytes;

using Musoq.DataSources.Search.Components.Traversal;

namespace Musoq.DataSources.Search.Tests.Components.Bytes;

[TestClass]
public sealed class SearchBinaryPolicyTests
{
    [TestMethod]
    public void NulMarkers_ShouldBeDetectedAtReaderBlockBoundaries()
    {
        var root = CreateTemporaryRoot();
        var offsets = new[]
        {
            SearchCharBuffer.RequestedLength - 1,
            SearchCharBuffer.RequestedLength,
            SearchCharBuffer.RequestedLength + 1
        };

        try
        {
            Directory.CreateDirectory(root);
            using var buffer = SearchCharBuffer.Rent();
            foreach (var offset in offsets)
            {
                var path = Path.Combine(root, $"marker-{offset}.bin");
                var bytes = Enumerable.Repeat((byte)'a', offset + 1).ToArray();
                bytes[offset] = SearchBinaryPolicy.BinaryMarker switch
                {
                    '\0' => (byte)0,
                    _ => throw new AssertFailedException("The binary marker must remain NUL.")
                };
                File.WriteAllBytes(path, bytes);

                Assert.IsTrue(
                    SearchBinaryPolicy.IsBinaryFile(
                        path,
                        SearchEncodingMode.Utf8,
                        buffer),
                    $"Expected a binary marker at byte offset {offset}.");
            }
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void Utf16Text_ShouldNotBeClassifiedByEncodedZeroBytes()
    {
        var root = CreateTemporaryRoot();

        try
        {
            Directory.CreateDirectory(root);
            using var buffer = SearchCharBuffer.Rent();
            foreach (var bigEndian in new[] { false, true })
            {
                var path = Path.Combine(root, bigEndian ? "utf16-be.txt" : "utf16-le.txt");
                var encoding = new UnicodeEncoding(
                    bigEndian,
                    byteOrderMark: true,
                    throwOnInvalidBytes: true);
                File.WriteAllBytes(
                    path,
                    encoding.GetPreamble().Concat(encoding.GetBytes("TODO Ω\n")).ToArray());

                Assert.IsFalse(
                    SearchBinaryPolicy.IsBinaryFile(
                        path,
                        SearchEncodingMode.Auto,
                        buffer),
                    path);
            }
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void ValidUtf8BinaryPayload_ShouldBeSkippedBeforeLateMatchesReachTheSink()
    {
        var root = CreateTemporaryRoot();
        var binaryPath = Path.Combine(root, "payload.bin");
        var textPath = Path.Combine(root, "visible.txt");

        try
        {
            Directory.CreateDirectory(root);
            var binaryPayload = Encoding.UTF8.GetBytes(
                "TODO before\n" +
                new string('x', SearchCharBuffer.RequestedLength + 1) +
                "\0TODO after\n");
            File.WriteAllBytes(binaryPath, binaryPayload);
            File.WriteAllText(
                textPath,
                "TODO visible\n",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            var counters = new SearchScopeCounters();
            var rows = new SearchMatchesSource(
                    SearchRequest.Create(root, "TODO"),
                    RuntimeV2TestContexts.CreateExecutionContext(),
                    scopeCounters: counters)
                .Chunks
                .SelectMany(static chunk => chunk)
                .ToArray();

            Assert.AreEqual(1, rows.Length);
            Assert.AreEqual("visible.txt", rows[0].Path);
            Assert.AreEqual(1L, counters.BinaryFilesSkipped);
            // Small files are classified and scanned from the same byte snapshot.
            Assert.AreEqual(2L, counters.ContentOpenAttempts);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void OpaqueControlPayloadWithNul_ShouldUseTheSameBinaryDecision()
    {
        var root = CreateTemporaryRoot();
        var path = Path.Combine(root, "opaque.bin");

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllBytes(path, [0, 1, 2, 3, (byte)'T', (byte)'O', (byte)'D', (byte)'O']);

            using var buffer = SearchCharBuffer.Rent();
            Assert.IsTrue(
                SearchBinaryPolicy.IsBinaryFile(
                    path,
                    SearchEncodingMode.Utf8,
                    buffer));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    private static string CreateTemporaryRoot()
    {
        return Path.Combine(Path.GetTempPath(), $"musoq-search-binary-{Guid.NewGuid():N}");
    }

    private static void DeleteTemporaryRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}
