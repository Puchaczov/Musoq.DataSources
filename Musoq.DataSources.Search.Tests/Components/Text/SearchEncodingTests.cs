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

using Musoq.DataSources.Search.Components.Diagnostics;

using Musoq.DataSources.Search.Components.Text;

namespace Musoq.DataSources.Search.Tests.Components.Text;

[TestClass]
public sealed class SearchEncodingTests
{
    [TestMethod]
    public void Auto_ShouldHonorSupportedBomsAndDefaultToStrictUtf8()
    {
        var root = CreateTemporaryRoot();

        try
        {
            Directory.CreateDirectory(root);
            WriteUtf8(root, "plain.txt", "TODO plain\n", includeBom: false);
            WriteUtf8(root, "utf8-bom.txt", "TODO utf8\n", includeBom: true);
            WriteUtf16(root, "utf16-le.txt", "TODO little\n", bigEndian: false);
            WriteUtf16(root, "utf16-be.txt", "TODO big\n", bigEndian: true);

            var rows = ReadMatches(root, "auto");

            CollectionAssert.AreEquivalent(
                new[] { "plain.txt", "utf8-bom.txt", "utf16-le.txt", "utf16-be.txt" },
                rows.Select(static row => row.Path).ToArray());
            Assert.IsTrue(rows.All(static row => row.MatchText == "TODO"));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void ExplicitModes_ShouldRequireTheirDeclaredBomShape()
    {
        var root = CreateTemporaryRoot();

        try
        {
            Directory.CreateDirectory(root);
            WriteUtf8(root, "plain.txt", "TODO plain\n", includeBom: false);
            WriteUtf8(root, "utf8-bom.txt", "TODO utf8\n", includeBom: true);
            WriteUtf16(root, "utf16-le.txt", "TODO little\n", bigEndian: false);
            WriteUtf16(root, "utf16-be.txt", "TODO big\n", bigEndian: true);

            Assert.AreEqual(1, ReadMatches(Path.Combine(root, "plain.txt"), "utf8").Length);
            Assert.AreEqual(1, ReadMatches(Path.Combine(root, "utf8-bom.txt"), "utf8-bom").Length);
            Assert.AreEqual(1, ReadMatches(Path.Combine(root, "utf16-le.txt"), "utf16-le-bom").Length);
            Assert.AreEqual(1, ReadMatches(Path.Combine(root, "utf16-be.txt"), "utf16-be-bom").Length);

            var missingBom = Assert.ThrowsException<SearchEncodingException>(() =>
                ReadMatches(Path.Combine(root, "plain.txt"), "utf8-bom"));
            Assert.AreEqual(SearchDiagnosticCodes.UnsupportedEncoding, missingBom.Diagnostic.Code);

            var mismatch = Assert.ThrowsException<SearchEncodingException>(() =>
                ReadMatches(Path.Combine(root, "utf16-be.txt"), "utf16-le-bom"));

            Assert.AreEqual(SearchDiagnosticCodes.UnsupportedEncoding, mismatch.Diagnostic.Code);
            Assert.AreEqual("encoding", mismatch.Diagnostic.Location?.ArgumentName);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void Auto_ShouldNotHeuristicallyDecodeUtf16WithoutABom()
    {
        var root = CreateTemporaryRoot();
        var path = Path.Combine(root, "utf16-without-bom.txt");

        try
        {
            Directory.CreateDirectory(root);
            var encoding = new UnicodeEncoding(
                bigEndian: false,
                byteOrderMark: false,
                throwOnInvalidBytes: true);
            File.WriteAllBytes(path, encoding.GetBytes("TODO\n"));

            var rows = ReadMatches(path, "auto");

            Assert.AreEqual(0, rows.Length);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void StrictUtf8_ShouldRejectPartialMultibyteSequenceAtEof()
    {
        var root = CreateTemporaryRoot();
        var path = Path.Combine(root, "invalid-utf8.txt");

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllBytes(path, [
                (byte)'T', (byte)'O', (byte)'D', (byte)'O', (byte)'\n',
                0xe2
            ]);

            var exception = Assert.ThrowsException<SearchSourceReadException>(() =>
                ReadMatches(path, "utf8"));

            Assert.AreEqual(SearchDiagnosticCodes.SourceReadFailed, exception.Diagnostic.Code);
            Assert.IsInstanceOfType<DecoderFallbackException>(exception.InnerException);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void StrictUtf16_ShouldRejectOddByteTail()
    {
        var root = CreateTemporaryRoot();
        var path = Path.Combine(root, "invalid-utf16.txt");

        try
        {
            Directory.CreateDirectory(root);
            var encoding = new UnicodeEncoding(
                bigEndian: false,
                byteOrderMark: true,
                throwOnInvalidBytes: true);
            var bytes = encoding.GetPreamble()
                .Concat(encoding.GetBytes("TODO\n"))
                .Concat(new byte[] { 0x41 })
                .ToArray();
            File.WriteAllBytes(path, bytes);

            var exception = Assert.ThrowsException<SearchSourceReadException>(() =>
                ReadMatches(path, "utf16-le-bom"));

            Assert.AreEqual(SearchDiagnosticCodes.SourceReadFailed, exception.Diagnostic.Code);
            Assert.IsInstanceOfType<DecoderFallbackException>(exception.InnerException);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void UnsupportedModesAndBomFamilies_ShouldBeRejectedPrecisely()
    {
        var root = CreateTemporaryRoot();
        var utf32Path = Path.Combine(root, "utf32.txt");

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllBytes(utf32Path, [0xff, 0xfe, 0x00, 0x00, 0x54, 0x00, 0x00, 0x00]);

            var bomException = Assert.ThrowsException<SearchEncodingException>(() =>
                ReadMatches(utf32Path, "auto"));
            Assert.AreEqual(SearchDiagnosticCodes.UnsupportedEncoding, bomException.Diagnostic.Code);

            var argumentException = Assert.ThrowsException<SearchEncodingException>(() =>
                SearchRequest.Create(root, "TODO", encoding: "ascii"));
            Assert.AreEqual(SearchDiagnosticCodes.UnsupportedEncoding, argumentException.Diagnostic.Code);

            var nullException = Assert.ThrowsException<SearchEncodingException>(() =>
                SearchDiagnosticValidation.ValidateEncoding(null));
            Assert.AreEqual(SearchDiagnosticCodes.InvalidArgument, nullException.Diagnostic.Code);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void ContractValues_ShouldRoundTripToCanonicalEncodingModes()
    {
        var values = new[] { "auto", "utf8", "utf8-bom", "utf16-le-bom", "utf16-be-bom" };

        foreach (var value in values)
        {
            var mode = SearchEncodingPolicy.Parse(value);

            Assert.AreEqual(value, SearchEncodingPolicy.ToContractValue(mode));
            SearchDiagnosticValidation.ValidateEncoding(value);
        }
    }

    private static SearchMatch[] ReadMatches(string root, string encoding)
    {
        return new SearchMatchesSource(
                SearchRequest.Create(root, "TODO", encoding: encoding),
                RuntimeV2TestContexts.CreateExecutionContext())
            .Chunks
            .SelectMany(static chunk => chunk)
            .ToArray();
    }

    private static void WriteUtf8(string root, string relativePath, string content, bool includeBom)
    {
        var encoding = new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: includeBom,
            throwOnInvalidBytes: true);
        var bytes = (includeBom ? encoding.GetPreamble() : Array.Empty<byte>())
            .Concat(encoding.GetBytes(content))
            .ToArray();
        File.WriteAllBytes(Path.Combine(root, relativePath), bytes);
    }

    private static void WriteUtf16(string root, string relativePath, string content, bool bigEndian)
    {
        var encoding = new UnicodeEncoding(
            bigEndian,
            byteOrderMark: true,
            throwOnInvalidBytes: true);
        var bytes = encoding.GetPreamble()
            .Concat(encoding.GetBytes(content))
            .ToArray();
        File.WriteAllBytes(Path.Combine(root, relativePath), bytes);
    }

    private static string CreateTemporaryRoot()
    {
        return Path.Combine(Path.GetTempPath(), $"musoq-search-encoding-{Guid.NewGuid():N}");
    }

    private static void DeleteTemporaryRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}
