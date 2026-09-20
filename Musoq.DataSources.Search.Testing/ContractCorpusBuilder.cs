#nullable enable

using System.Security.Cryptography;
using System.Text;

namespace Musoq.DataSources.Search.Testing;

public static class ContractCorpusBuilder
{
    public static ContractCorpusFixture Create(
        string manifestPath,
        ContractCorpusProfile profile = ContractCorpusProfile.All,
        uint? seed = null)
    {
        var manifest = ContractCorpusManifest.Load(manifestPath);
        var effectiveSeed = seed ?? manifest.Seed;
        var root = Path.Combine(Path.GetTempPath(), $"musoq-search-contract-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var generated = new List<ContractCorpusFile>();
            foreach (var spec in manifest.Select(profile))
            {
                var bytes = CreateBytes(spec, effectiveSeed);
                var path = GetSafePath(root, spec.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, bytes);
                ApplyMetadata(path, spec, bytes.LongLength);
                ApplyHiddenAttributes(path, spec);
                generated.Add(new ContractCorpusFile(spec, bytes));
            }

            if (profile is ContractCorpusProfile.Scale or ContractCorpusProfile.All)
                GenerateScaleFiles(root, manifest.ScaleFileCount, effectiveSeed, generated);

            return new ContractCorpusFixture(root, manifest, profile, effectiveSeed, generated);
        }
        catch
        {
            TryDelete(root);
            throw;
        }
    }

    private static byte[] CreateBytes(ContractCorpusFileSpec spec, uint seed)
    {
        return spec.Recipe switch
        {
            "top" => Utf8("TODO TODO\n"),
            "empty" => [],
            "none" => Utf8("DONE\n"),
            "child" => Utf8("before\nTODO\nafter\n"),
            "overlap" => Utf8("ababa"),
            "unicode" => Utf8("😀 TODO\nnext TODO"),
            "crlf" => Utf8("TODO\r\nDONE\r\nTODO"),
            "case" => Utf8("todo TODO ToDo TODOX _TODO TODO_\n"),
            "boundary" => Utf8(new string('x', 8_190) + "TODO\n"),
            "long-literal" => Utf8(new string('x', 1_048_700) + "TODO\n"),
            "unterminated" => Utf8("prefix TODO"),
            "bom" => AddPreamble(new UTF8Encoding(true), "TODO\n"),
            "utf16-le" => AddPreamble(Encoding.Unicode, "TODO\n"),
            "utf16-be" => AddPreamble(Encoding.BigEndianUnicode, "TODO\n"),
            "late-nul" => CreateLateNul(),
            "invalid-utf8" => [0x54, 0x4f, 0x44, 0x4f, 0x00, 0xff, 0xfe],
            "issues" => Utf8("TODO before ISSUE-42 after\nTODO next ISSUE-7\n"),
            "captures" => Utf8("name=alpha id=42\nname=beta id=7\n"),
            "empty-group" => Utf8("ab\n"),
            "gitignore" => Utf8("ignored/*\n!ignored/kept.txt\n*.skip\n"),
            "scope-kept" => Utf8("TODO\n"),
            "scope-ignored" => Utf8("TODO SECRET\n"),
            "scope-negated" => Utf8("TODO NEGATED\n"),
            "payload" => [0x00, 0x11, 0x54, 0x4f, 0x44, 0x4f, 0x22, 0x33, 0x54, 0x4f, 0x44, 0x4f, 0x44],
            "byte-boundary" => CreateByteBoundary(),
            "combining" => Utf8("e\u0301 TODO 😀 TODO\n"),
            "supplementary" => Utf8("😀😀TODO\n"),
            "lone-cr" => Utf8("TODO\rDONE\rTODO"),
            "dense" => Utf8("TODO TODO TODO TODO\nTODOTODO\n"),
            "adjacent" => Utf8("TODOFIXMETODO\n"),
            "long-regex" => Utf8(new string('x', 1_048_700) + "TODO\n"),
            "malformed-utf16-le" => [0xFF, 0xFE, 0x54, 0x00, 0x4F],
            "malformed-utf16-be" => [0xFE, 0xFF, 0x00, 0x54, 0x00],
            "bounded" => Utf8("BEGIN\nTODO\nEND\nBEGIN\nDONE\nEND\n"),
            "bounded-unterminated" => Utf8("BEGIN\nunfinished\nTODO\n"),
            "optional-captures" => Utf8("name=alpha id=\nname=beta\n"),
            "nested-gitignore" => Utf8("*.tmp\n!keep.tmp\n"),
            "nested-ignore" => Utf8("*.secret\n"),
            "nested-rgignore" => Utf8("*.rgskip\n"),
            "metadata-small" => Utf8("TODO\n"),
            "metadata-large" => Utf8("TODO TODO TODO TODO TODO TODO\n"),
            "byte-start" => [0x54, 0x4F, 0x44, 0x4F, 0x01],
            "byte-end" => [0x01, 0x54, 0x4F, 0x44, 0x4F],
            "byte-middle" => [0x01, 0x02, 0x54, 0x4F, 0x44, 0x4F, 0x03],
            _ => throw new InvalidDataException($"Unknown contract corpus recipe '{spec.Recipe}'.")
        };
    }

    private static void GenerateScaleFiles(
        string root,
        int count,
        uint seed,
        ICollection<ContractCorpusFile> generated)
    {
        var random = new XorShift32(seed ^ 0x9e3779b9u);
        for (var index = 0; index < count; index++)
        {
            var relativePath = $"scale/batch-{index / 8:00}/file-{index:000}.txt";
            var marker = index % 5 == 0 ? "TODO" : "DONE";
            var filler = new string((char)('a' + random.Next(26)), 32 + random.Next(96));
            var bytes = Utf8($"file={index:000} {marker} {filler}\n");
            var path = GetSafePath(root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, bytes);
            generated.Add(new ContractCorpusFile(
                new ContractCorpusFileSpec
                {
                    Profile = "scale",
                    Path = relativePath,
                    Kind = "text",
                    Encoding = "utf8",
                    Recipe = "scale",
                },
                bytes));
        }
    }

    private static byte[] CreateLateNul()
    {
        var prefix = Utf8(new string('x', 64) + " TODO before\n");
        var suffix = Utf8("TODO after\n");
        return prefix.Concat(new byte[] { 0 }).Concat(suffix).ToArray();
    }

    private static byte[] CreateByteBoundary()
    {
        var bytes = new byte[8_197];
        Array.Fill(bytes, (byte)0x11);
        bytes[8_191] = 0x54;
        bytes[8_192] = 0x4f;
        bytes[8_193] = 0x44;
        bytes[8_194] = 0x4f;
        bytes[8_195] = 0xee;
        bytes[8_196] = 0xff;
        return bytes;
    }

    private static byte[] AddPreamble(Encoding encoding, string value)
    {
        return encoding.GetPreamble().Concat(encoding.GetBytes(value)).ToArray();
    }

    private static byte[] Utf8(string value)
    {
        return new UTF8Encoding(false).GetBytes(value);
    }

    private static string GetSafePath(string root, string relativePath)
    {
        var combined = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!combined.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Corpus path '{relativePath}' escapes '{root}'.");

        return combined;
    }

    private static void ApplyHiddenAttributes(string path, ContractCorpusFileSpec spec)
    {
        if (!spec.Hidden)
            return;

        try
        {
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.Hidden);
            var directory = Path.GetDirectoryName(path);
            while (!string.IsNullOrEmpty(directory) && directory.Contains(".hidden-dir", StringComparison.Ordinal))
            {
                File.SetAttributes(directory, File.GetAttributes(directory) | FileAttributes.Hidden);
                directory = Path.GetDirectoryName(directory);
            }
        }
        catch (PlatformNotSupportedException)
        {
            // Dot-prefixed paths remain portable hidden-entry cases on Unix-like systems.
        }
    }

    private static void ApplyMetadata(string path, ContractCorpusFileSpec spec, long actualLength)
    {
        if (spec.ExpectedLength is { } expectedLength && expectedLength != actualLength)
        {
            throw new InvalidDataException(
                $"Corpus recipe '{spec.Recipe}' for '{spec.Path}' produced {actualLength} bytes; expected {expectedLength}.");
        }

        if (spec.ModifiedUtcTicks is { } ticks)
            File.SetLastWriteTimeUtc(path, new DateTime(ticks, DateTimeKind.Utc));
    }

    internal static void TryDelete(string root)
    {
        try
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
        catch
        {
            // Cleanup must not hide the original test failure.
        }
    }

    private sealed class XorShift32(uint seed)
    {
        private uint _state = seed == 0 ? 0x6d2b79f5u : seed;

        public int Next(int exclusiveMax)
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return (int)(_state % (uint)exclusiveMax);
        }
    }
}

public sealed class ContractCorpusFile
{
    public ContractCorpusFile(ContractCorpusFileSpec specification, byte[] bytes)
    {
        Specification = specification;
        Bytes = bytes.ToArray();
        Sha256 = Convert.ToHexString(SHA256.HashData(Bytes)).ToLowerInvariant();
    }

    public ContractCorpusFileSpec Specification { get; }

    public byte[] Bytes { get; }

    public string Sha256 { get; }

    public string RelativePath => Specification.Path;

    public bool IsText => string.Equals(Specification.Kind, "text", StringComparison.Ordinal);

    public bool IsBinary => string.Equals(Specification.Kind, "binary", StringComparison.Ordinal);
}

public sealed class ContractCorpusFixture : IDisposable
{
    private bool _disposed;

    internal ContractCorpusFixture(
        string root,
        ContractCorpusManifest manifest,
        ContractCorpusProfile profile,
        uint seed,
        IReadOnlyList<ContractCorpusFile> files)
    {
        Root = root;
        Manifest = manifest;
        Profile = profile;
        Seed = seed;
        Files = files;
    }

    public string Root { get; }

    public ContractCorpusManifest Manifest { get; }

    public ContractCorpusProfile Profile { get; }

    public uint Seed { get; }

    public IReadOnlyList<ContractCorpusFile> Files { get; }

    public bool KeepOnDispose { get; set; }

    public string FailureReportPath => Path.Combine(Root, "_search-contract-failure.txt");

    public string PathFor(string relativePath)
    {
        var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(
            Root,
            relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar)));
        var normalizedRoot = System.IO.Path.GetFullPath(Root).TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
        if (!path.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Path '{relativePath}' escapes the corpus root.");

        return path;
    }

    public string ReadUtf8(string relativePath)
    {
        return File.ReadAllText(PathFor(relativePath), new UTF8Encoding(false));
    }

    public void WriteFailureReport(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var builder = new StringBuilder()
            .AppendLine("Musoq Search contract fixture")
            .AppendLine($"Root: {Root}")
            .AppendLine($"Profile: {Profile}")
            .AppendLine($"Seed: {Seed}")
            .AppendLine($"ManifestHash: {Manifest.ManifestHash}")
            .AppendLine("Files:");

        foreach (var file in Files.OrderBy(file => file.RelativePath, StringComparer.Ordinal))
        {
            builder.AppendLine($"  {file.RelativePath} {file.Sha256}");
        }

        builder.AppendLine("Exception:")
            .AppendLine(exception.ToString());

        if (exception.Data["SearchQuery"] is { } query)
            builder.AppendLine($"Query: {query}");
        if (exception.Data["SearchRuntimeSettings"] is { } settings)
            builder.AppendLine($"RuntimeSettings: {settings}");
        if (exception.Data["SearchDiagnostic"] is { } diagnostic)
            builder.AppendLine($"Diagnostic: {diagnostic}");

        try
        {
            File.WriteAllText(FailureReportPath, builder.ToString(), new UTF8Encoding(false));
        }
        catch
        {
            // The original test exception is more useful than a report-write
            // failure. The fixture root is still retained for inspection.
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (!KeepOnDispose)
            ContractCorpusBuilder.TryDelete(Root);
    }
}
