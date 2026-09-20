#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Musoq.DataSources.Search.Tests.Infrastructure;

[TestClass]
public sealed class SyntheticSearchCorpusTests
{
    private static string ManifestPath => Path.Combine(
        AppContext.BaseDirectory,
        "TestData",
        "SyntheticSearchCorpus",
        "manifest.json");

    [TestMethod]
    public void RegenerateCorpus_ShouldMatchManifestAndRemainByteDeterministic()
    {
        var manifest = SyntheticSearchCorpusManifest.Load(ManifestPath);
        Assert.AreEqual(1, manifest.FormatVersion);
        Assert.AreEqual("xorshift32-v1", manifest.Generator);
        Assert.AreEqual("TODO", manifest.Literal);
        Assert.IsTrue(manifest.PlatformPrerequisites.TryGetValue("all", out var allPrerequisites));
        Assert.IsNotNull(allPrerequisites);
        Assert.IsTrue(allPrerequisites!.Count > 0);

        var firstRoot = CreateTemporaryRoot();
        var secondRoot = CreateTemporaryRoot();

        try
        {
            var first = SyntheticSearchCorpus.Generate(manifest, firstRoot);
            var second = SyntheticSearchCorpus.Generate(manifest, secondRoot);

            Assert.AreEqual(manifest.Units.GeneratedFileCount, first.Count);
            Assert.AreEqual(first.Count, second.Count);
            CollectionAssert.AreEqual(
                first.Select(file => file.Signature).ToArray(),
                second.Select(file => file.Signature).ToArray());

            Assert.AreEqual(
                manifest.Units.ExpectedTotalBytes,
                first.Sum(file => (long)file.ByteLength));
            Assert.AreEqual(
                manifest.Units.TextFileCount,
                first.Count(file => file.Kind == "text"));
            Assert.AreEqual(
                manifest.Units.BinaryFileCount,
                first.Count(file => file.Kind == "binary"));
            Assert.AreEqual(
                manifest.Units.IgnoreRuleFileCount,
                first.Count(file => file.Kind == "ignore"));

            ValidateManifestFiles(manifest, firstRoot, first);
            ValidateGeneratedGroups(manifest, firstRoot, first);
        }
        finally
        {
            DeleteTemporaryRoot(firstRoot);
            DeleteTemporaryRoot(secondRoot);
        }
    }

    private static void ValidateManifestFiles(
        SyntheticSearchCorpusManifest manifest,
        string root,
        IReadOnlyList<GeneratedSearchFile> generated)
    {
        var generatedByPath = generated.ToDictionary(file => file.RelativePath, StringComparer.Ordinal);
        var literalOccurrences = 0;
        var byteOccurrences = 0;

        foreach (var specification in manifest.Files)
        {
            Assert.IsTrue(generatedByPath.TryGetValue(specification.Path, out var generatedFile));
            Assert.IsNotNull(generatedFile);
            Assert.AreEqual(specification.ExpectedBytes, generatedFile!.ByteLength, specification.Path);

            var bytes = File.ReadAllBytes(ToFilePath(root, specification.Path));
            Assert.AreEqual(
                generatedFile.Sha256,
                Convert.ToHexStringLower(SHA256.HashData(bytes)),
                specification.Path);

            if (specification.Kind == "text")
            {
                var spans = CorpusOracle.FindTextSpans(bytes, specification.Encoding, manifest.Literal);
                CollectionAssert.AreEqual(
                    specification.ExpectedUnits.Utf16Spans.ToArray(),
                    spans.ToArray(),
                    specification.Path);
                Assert.AreEqual(
                    specification.ExpectedUnits.LiteralOccurrences,
                    spans.Count,
                    specification.Path);
                literalOccurrences += spans.Count;
            }
            else if (specification.Kind == "binary")
            {
                var spans = CorpusOracle.FindByteSpans(bytes, Encoding.ASCII.GetBytes(manifest.Literal));
                CollectionAssert.AreEqual(
                    specification.ExpectedUnits.ByteSpans.ToArray(),
                    spans.ToArray(),
                    specification.Path);
                Assert.AreEqual(
                    specification.ExpectedUnits.ByteOccurrences,
                    spans.Count,
                    specification.Path);
                byteOccurrences += spans.Count;
            }
        }

        Assert.AreEqual(
            manifest.Files.Sum(file => file.ExpectedUnits.LiteralOccurrences),
            literalOccurrences);
        Assert.AreEqual(
            manifest.Files.Sum(file => file.ExpectedUnits.ByteOccurrences),
            byteOccurrences);
    }

    private static void ValidateGeneratedGroups(
        SyntheticSearchCorpusManifest manifest,
        string root,
        IReadOnlyList<GeneratedSearchFile> generated)
    {
        var literalOccurrences = 0;
        var byteOccurrences = 0;

        foreach (var group in manifest.Groups)
        {
            var files = generated
                .Where(file => file.RelativePath.StartsWith(group.PathPrefix, StringComparison.Ordinal))
                .ToArray();

            Assert.AreEqual(group.Count, files.Length, group.PathPrefix);
            Assert.AreEqual(
                group.ExpectedTotalBytes,
                files.Sum(file => (long)file.ByteLength),
                group.PathPrefix);

            if (group.Kind == "text")
            {
                var occurrences = files.Sum(file =>
                    CorpusOracle.FindTextSpans(
                        File.ReadAllBytes(ToFilePath(root, file.RelativePath)),
                        group.Encoding,
                        manifest.Literal).Count);
                Assert.AreEqual(group.ExpectedLiteralOccurrences, occurrences, group.PathPrefix);
                literalOccurrences += occurrences;
            }
            else
            {
                var spans = CorpusOracle.FindByteSpans(
                    File.ReadAllBytes(ToFilePath(root, files.Single().RelativePath)),
                    Encoding.ASCII.GetBytes(manifest.Literal));
                CollectionAssert.AreEqual(group.ExpectedByteSpans.ToArray(), spans.ToArray(), group.PathPrefix);
                Assert.AreEqual(group.ExpectedByteOccurrences, spans.Count, group.PathPrefix);
                byteOccurrences += spans.Count;
            }
        }

        Assert.AreEqual(
            manifest.Units.ExpectedLiteralOccurrences,
            manifest.Files.Sum(file => file.ExpectedUnits.LiteralOccurrences) + literalOccurrences);
        Assert.AreEqual(
            manifest.Units.ExpectedByteOccurrences,
            manifest.Files.Sum(file => file.ExpectedUnits.ByteOccurrences) + byteOccurrences);
    }

    private static string CreateTemporaryRoot()
    {
        return Path.Combine(Path.GetTempPath(), $"musoq-search-corpus-{Guid.NewGuid():N}");
    }

    private static string ToFilePath(string root, string relativePath)
    {
        return Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static void DeleteTemporaryRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}

internal sealed class SyntheticSearchCorpusManifest
{
    public int FormatVersion { get; set; }

    public uint Seed { get; set; }

    public string Generator { get; set; } = string.Empty;

    public string Literal { get; set; } = string.Empty;

    public string RootName { get; set; } = string.Empty;

    public Dictionary<string, List<string>> PlatformPrerequisites { get; set; } = new(StringComparer.Ordinal);

    public SyntheticCorpusUnits Units { get; set; } = new();

    public List<SyntheticCorpusFile> Files { get; set; } = [];

    public List<SyntheticCorpusGroup> Groups { get; set; } = [];

    public static SyntheticSearchCorpusManifest Load(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<SyntheticSearchCorpusManifest>(
                   stream,
                   new JsonSerializerOptions
                   {
                       PropertyNameCaseInsensitive = true
                   })
               ?? throw new InvalidDataException($"Synthetic Search manifest '{path}' was empty.");
    }
}

internal sealed class SyntheticCorpusUnits
{
    public int GeneratedFileCount { get; set; }

    public int TextFileCount { get; set; }

    public int BinaryFileCount { get; set; }

    public int IgnoreRuleFileCount { get; set; }

    public long ExpectedTotalBytes { get; set; }

    public int ExpectedLiteralOccurrences { get; set; }

    public int ExpectedByteOccurrences { get; set; }
}

internal sealed class SyntheticCorpusFile
{
    public string Path { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public string Encoding { get; set; } = string.Empty;

    public string Recipe { get; set; } = string.Empty;

    public bool Ignored { get; set; }

    public int ExpectedBytes { get; set; }

    public SyntheticExpectedUnits ExpectedUnits { get; set; } = new();
}

internal sealed class SyntheticExpectedUnits
{
    public int LiteralOccurrences { get; set; }

    public int ByteOccurrences { get; set; }

    public List<CorpusSpan> Utf16Spans { get; set; } = [];

    public List<CorpusSpan> ByteSpans { get; set; } = [];
}

internal sealed class SyntheticCorpusGroup
{
    public string PathTemplate { get; set; } = string.Empty;

    public string PathPrefix { get; set; } = string.Empty;

    public int Count { get; set; }

    public string Kind { get; set; } = string.Empty;

    public string Encoding { get; set; } = string.Empty;

    public string Recipe { get; set; } = string.Empty;

    public long ExpectedTotalBytes { get; set; }

    public int ExpectedLiteralOccurrences { get; set; }

    public int ExpectedByteOccurrences { get; set; }

    public List<CorpusSpan> ExpectedByteSpans { get; set; } = [];
}

internal static class SyntheticSearchCorpus
{
    public static IReadOnlyList<GeneratedSearchFile> Generate(
        SyntheticSearchCorpusManifest manifest,
        string root)
    {
        Directory.CreateDirectory(root);
        var generated = new List<GeneratedSearchFile>();

        foreach (var file in manifest.Files)
            generated.Add(WriteFile(root, file.Path, file.Kind, Render(file.Recipe, manifest.Seed, 0)));

        foreach (var group in manifest.Groups)
        {
            for (var index = 0; index < group.Count; index++)
            {
                var path = ExpandPath(group.PathTemplate, index);
                generated.Add(WriteFile(root, path, group.Kind, Render(group.Recipe, manifest.Seed, index)));
            }
        }

        return generated;
    }

    private static GeneratedSearchFile WriteFile(string root, string relativePath, string kind, byte[] bytes)
    {
        if (Path.IsPathRooted(relativePath) || relativePath.Split('/').Any(part => part is "" or "." or ".."))
            throw new InvalidDataException($"Synthetic path '{relativePath}' is not a safe relative path.");

        var path = ToFilePath(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
        return new GeneratedSearchFile(
            relativePath,
            kind,
            bytes.Length,
            Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    private static byte[] Render(string recipe, uint seed, int index)
    {
        return recipe switch
        {
            "truth-sparse" => Utf8("TODO one\nquiet\nTODO two\n"),
            "truth-dense" => Utf8(string.Join(" ", Enumerable.Repeat("TODO", 16)) + "\n"),
            "truth-giant-line" => Utf8(new string('x', 9_000) + "TODO\n"),
            "truth-unicode" => Utf8("😀 café cafe\u0301 TODO\n"),
            "truth-no-match" => Utf8("DONE\n"),
            "ignore-rules" => Utf8("ignored/\n"),
            "scope-kept" => Utf8("TODO kept\n"),
            "scope-ignored" => Utf8("TODO hidden\n"),
            "encoding-utf8-bom" => Utf8("TODO utf8-bom\n", includeBom: true),
            "encoding-utf16-le" => Utf16("TODO utf16-le\n", bigEndian: false),
            "encoding-utf16-be" => Utf16("TODO utf16-be\n", bigEndian: true),
            "binary-raw" => [0x00, 0x11, (byte)'T', (byte)'O', (byte)'D', (byte)'O', 0xff, 0x00],
            "scale-text" => RenderScaleText(seed, index),
            "scale-binary" => RenderScaleBinary(seed),
            _ => throw new InvalidDataException($"Unknown synthetic corpus recipe '{recipe}'.")
        };
    }

    private static byte[] RenderScaleText(uint seed, int index)
    {
        var random = new XorShift32(unchecked(seed + (uint)index * 0x9e3779b9u));
        var builder = new StringBuilder();
        builder.Append(random.Next().ToString("X8", CultureInfo.InvariantCulture));
        builder.Append(' ');
        if (index % 5 == 0)
            builder.Append("TODO ");
        if (index % 7 == 0)
            builder.Append("TODO ");
        builder.Append("tail\n");
        return Utf8(builder.ToString());
    }

    private static byte[] RenderScaleBinary(uint seed)
    {
        var bytes = new byte[16_384];
        var random = new XorShift32(seed);
        for (var index = 0; index < bytes.Length; index++)
            bytes[index] = (byte)('a' + random.Next() % 26);

        WriteAscii(bytes, 127, "TODO");
        WriteAscii(bytes, 8_191, "TODO");
        WriteAscii(bytes, 16_380, "TODO");
        return bytes;
    }

    private static void WriteAscii(byte[] destination, int offset, string value)
    {
        Buffer.BlockCopy(Encoding.ASCII.GetBytes(value), 0, destination, offset, value.Length);
    }

    private static byte[] Utf8(string value, bool includeBom = false)
    {
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: includeBom, throwOnInvalidBytes: true);
        var content = encoding.GetBytes(value);
        return includeBom ? encoding.GetPreamble().Concat(content).ToArray() : content;
    }

    private static byte[] Utf16(string value, bool bigEndian)
    {
        var encoding = new UnicodeEncoding(bigEndian, byteOrderMark: true, throwOnInvalidBytes: true);
        return encoding.GetPreamble().Concat(encoding.GetBytes(value)).ToArray();
    }

    private static string ExpandPath(string template, int index)
    {
        return template
            .Replace(
                "{batch:00}",
                (index % 4).ToString("00", CultureInfo.InvariantCulture),
                StringComparison.Ordinal)
            .Replace(
                "{index:000}",
                index.ToString("000", CultureInfo.InvariantCulture),
                StringComparison.Ordinal);
    }

    private static string ToFilePath(string root, string relativePath)
    {
        return Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private sealed class XorShift32(uint seed)
    {
        private uint _state = seed == 0 ? 0x6d2b79f5u : seed;

        public uint Next()
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return _state;
        }
    }
}

internal sealed record GeneratedSearchFile(
    string RelativePath,
    string Kind,
    int ByteLength,
    string Sha256)
{
    public string Signature => $"{RelativePath}|{Kind}|{ByteLength}|{Sha256}";
}

internal readonly record struct CorpusSpan(int Start, int Length);

internal static class CorpusOracle
{
    public static IReadOnlyList<CorpusSpan> FindTextSpans(
        byte[] bytes,
        string encodingName,
        string literal)
    {
        var text = Decode(bytes, encodingName);
        var spans = new List<CorpusSpan>();
        var start = 0;

        while (start <= text.Length - literal.Length)
        {
            var match = text.IndexOf(literal, start, StringComparison.Ordinal);
            if (match < 0)
                break;

            spans.Add(new CorpusSpan(match, literal.Length));
            start = match + literal.Length;
        }

        return spans;
    }

    public static IReadOnlyList<CorpusSpan> FindByteSpans(byte[] bytes, byte[] pattern)
    {
        var spans = new List<CorpusSpan>();
        for (var start = 0; start <= bytes.Length - pattern.Length; start++)
        {
            if (!bytes.AsSpan(start, pattern.Length).SequenceEqual(pattern))
                continue;

            spans.Add(new CorpusSpan(start, pattern.Length));
            start += pattern.Length - 1;
        }

        return spans;
    }

    private static string Decode(byte[] bytes, string encodingName)
    {
        Encoding encoding = encodingName switch
        {
            "utf8" or "utf8-bom" => new UTF8Encoding(false, true),
            "utf16-le-bom" => new UnicodeEncoding(false, true, true),
            "utf16-be-bom" => new UnicodeEncoding(true, true, true),
            _ => throw new InvalidDataException($"Unsupported text encoding '{encodingName}'.")
        };
        var text = encoding.GetString(bytes);
        return text.Length > 0 && text[0] == '\ufeff' ? text[1..] : text;
    }
}
