#nullable enable

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Musoq.DataSources.Search.Testing;

public enum ContractCorpusProfile
{
    Minimal,
    Text,
    Encoding,
    Regex,
    Scope,
    Bytes,
    Extended,
    Scale,
    All
}

public sealed class ContractCorpusManifest
{
    public ContractCorpusManifest()
    {
    }

    [JsonPropertyName("formatVersion")]
    public int FormatVersion { get; set; }

    [JsonPropertyName("seed")]
    public uint Seed { get; set; }

    [JsonPropertyName("generator")]
    public string Generator { get; set; } = string.Empty;

    [JsonPropertyName("scaleFileCount")]
    public int ScaleFileCount { get; set; }

    [JsonPropertyName("files")]
    public List<ContractCorpusFileSpec> Files { get; set; } = [];

    public string ManifestHash { get; private set; } = string.Empty;

    public static ContractCorpusManifest Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var bytes = File.ReadAllBytes(path);
        var manifest = JsonSerializer.Deserialize<ContractCorpusManifest>(bytes)
                       ?? throw new InvalidDataException($"Could not deserialize corpus manifest '{path}'.");

        manifest.Validate(path);
        manifest.ManifestHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return manifest;
    }

    public IReadOnlyList<ContractCorpusFileSpec> Select(ContractCorpusProfile profile)
    {
        return Files
            .Where(file => Includes(profile, file.Profile))
            .OrderBy(file => file.Path, StringComparer.Ordinal)
            .ToArray();
    }

    private void Validate(string path)
    {
        if (FormatVersion != 1)
            throw new InvalidDataException($"Unsupported contract corpus format version {FormatVersion} in '{path}'.");

        if (!string.Equals(Generator, "search-contract-v1", StringComparison.Ordinal) &&
            !string.Equals(Generator, "search-contract-extended-v1", StringComparison.Ordinal))
            throw new InvalidDataException($"Unexpected contract corpus generator '{Generator}'.");

        if (ScaleFileCount < 1)
            throw new InvalidDataException("The contract corpus must define at least one scale file.");

        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Files)
        {
            if (string.IsNullOrWhiteSpace(file.Path))
                throw new InvalidDataException("Corpus file paths cannot be empty.");

            var normalized = file.Path.Replace('\\', '/');
            if (!string.Equals(normalized, file.Path, StringComparison.Ordinal))
                throw new InvalidDataException($"Corpus path '{file.Path}' must use '/'.");

            if (Path.IsPathRooted(file.Path) || normalized.StartsWith("../", StringComparison.Ordinal) ||
                normalized.Contains("/../", StringComparison.Ordinal) || normalized.Equals("..", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Corpus path '{file.Path}' escapes the fixture root.");
            }

            if (!paths.Add(file.Path))
                throw new InvalidDataException($"Corpus path '{file.Path}' is duplicated.");

            if (file.Profile is not ("minimal" or "text" or "encoding" or "regex" or "scope" or "bytes" or
                "text-extended" or "encoding-extended" or "regex-extended" or "scope-extended" or "bytes-extended"))
                throw new InvalidDataException($"Corpus path '{file.Path}' has an unknown profile '{file.Profile}'.");

            if (string.IsNullOrWhiteSpace(file.Recipe))
                throw new InvalidDataException($"Corpus path '{file.Path}' has no recipe.");
        }
    }

    private static bool Includes(ContractCorpusProfile requested, string fileProfile)
    {
        return requested switch
        {
            ContractCorpusProfile.Minimal => fileProfile == "minimal",
            ContractCorpusProfile.Text => fileProfile is "minimal" or "text",
            ContractCorpusProfile.Encoding => fileProfile is "minimal" or "encoding",
            ContractCorpusProfile.Regex => fileProfile is "minimal" or "text" or "regex",
            ContractCorpusProfile.Scope => fileProfile is "minimal" or "scope",
            ContractCorpusProfile.Bytes => fileProfile is "minimal" or "bytes",
            ContractCorpusProfile.Extended => fileProfile.EndsWith("-extended", StringComparison.Ordinal),
            ContractCorpusProfile.Scale => fileProfile == "minimal",
            ContractCorpusProfile.All => true,
            _ => throw new ArgumentOutOfRangeException(nameof(requested), requested, null)
        };
    }
}

public sealed class ContractCorpusFileSpec
{
    [JsonPropertyName("profile")]
    public string Profile { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("encoding")]
    public string Encoding { get; set; } = string.Empty;

    [JsonPropertyName("recipe")]
    public string Recipe { get; set; } = string.Empty;

    [JsonPropertyName("ignored")]
    public bool Ignored { get; set; }

    [JsonPropertyName("hidden")]
    public bool Hidden { get; set; }

    [JsonPropertyName("modifiedUtcTicks")]
    public long? ModifiedUtcTicks { get; set; }

    [JsonPropertyName("expectedLength")]
    public long? ExpectedLength { get; set; }

    [JsonPropertyName("metadataTag")]
    public string? MetadataTag { get; set; }
}
