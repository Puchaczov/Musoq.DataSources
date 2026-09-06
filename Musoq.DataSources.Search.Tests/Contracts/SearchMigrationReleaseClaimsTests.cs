#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Search.Tests.Contracts;

[TestClass]
public sealed class SearchMigrationReleaseClaimsTests
{
    private static readonly string[] ExpectedMethods =
    [
        "matches",
        "many",
        "lines",
        "files",
        "counts",
        "paths",
        "bytes",
        "audit"
    ];

    [TestMethod]
    public void ReleaseNotes_ShouldMatchPackageRegistryAndRuntimeSourceSnapshot()
    {
        var notes = Read("docs", "search", "search-migration-and-release-notes-v1.md");
        using var packageRegistry = Load("scripts", "release", "packages.json");
        var package = packageRegistry.RootElement
            .GetProperty("packages")
            .EnumerateArray()
            .Single(item => item.GetProperty("packageId").GetString() == "Musoq.DataSources.Search");

        StringAssert.Contains(notes, $"`{package.GetProperty("packageId").GetString()}`");
        StringAssert.Contains(notes, package.GetProperty("version").GetString()!);
        StringAssert.Contains(notes, "`net10.0`");

        var metadataContext = new SourceMetadataContext(
            "migration-release-claims",
            default,
            [],
            new Dictionary<string, string>(),
            NullLogger.Instance);
        var schema = new SearchSchema();
        var methods = schema.GetRawConstructors(metadataContext)
            .Select(static method => method.MethodName)
            .ToArray();

        CollectionAssert.AreEqual(ExpectedMethods, methods);
        foreach (var method in methods)
        {
            StringAssert.Contains(notes, $"`search.{method}(");
            var descriptor = schema.DescribeSource(
                method,
                new SourceDescribeContext(
                    new SourceIdentity("search", method, "migration-release-claims", method),
                    metadataContext),
                SourceArguments(method));

            Assert.IsTrue(descriptor.Columns.Count > 0, $"Runtime description for search.{method} has no columns.");
        }
    }

    [TestMethod]
    public void ReleaseNotes_ShouldLinkTheSupportingContractsAndMeasurements()
    {
        var notes = Read("docs", "search", "search-migration-and-release-notes-v1.md");
        var normalizedNotes = NormalizeWhitespace(notes);
        var supportingFiles = new[]
        {
            "docs/search/search-capability-card-v1.md",
            "docs/search/search-regex-backend-v1.md",
            "docs/search/search-scope-contract-v1.md",
            "docs/search/search-coordinate-policy-v1.md",
            "docs/search/search-byte-pattern-contract-v1.md",
            "docs/search/search-completion-contract-v1.md",
            "docs/search/search-log-search-to-parse-v1.md",
            "docs/search/search-interpretation-composition-v1.md",
            "docs/search/search-migration-configuration-recipes-v1.md",
            "RepresentativeQueries.md",
            "docs/search/search-benchmark-agent-evaluation-results-v1.md",
            "docs/search/search-parity-benchmark-results-v1.md",
            "docs/search/search-delivery-latency-results-v1.md",
            "docs/search/search-resource-stability-results-v1.md",
            "scripts/release/packages.json"
        };

        foreach (var path in supportingFiles)
        {
            Assert.IsTrue(File.Exists(PathFromRoot(path)), $"Missing supporting contract or measurement '{path}'.");
            StringAssert.Contains(notes, Path.GetFileName(path), $"Release note does not name '{path}'.");
        }

        StringAssert.Contains(normalizedNotes, "The existing `os.files` and `flat.file` datasource behaviors are unchanged");
        StringAssert.Contains(normalizedNotes, "broad shared traversal refactoring is deliberately outside");
        StringAssert.Contains(normalizedNotes, "generated XML");
        StringAssert.Contains(normalizedNotes, "Runtime-v2");
    }

    [TestMethod]
    public void ReleaseNotes_ShouldAnswerUnsupportedFeatureQuestionsWithoutOverclaiming()
    {
        var notes = Read("docs", "search", "search-migration-and-release-notes-v1.md");
        var normalizedNotes = NormalizeWhitespace(notes);
        var safeAnswers = new[]
        {
            "No. Use `search.counts` and `search.audit`",
            "No. Regex is parser-only for this source",
            "No. The public shape is a scalar JSON request",
            "No. They are original-byte coordinates",
            "No. Search returns lexical candidates",
            "No. The host version, package dependencies and assembly-resolution boundary",
            "No. This is additive; their existing contracts remain",
            "No. They cover the recorded current-platform cells only",
            "No. Local package and release metadata are not publication evidence"
        };

        StringAssert.Contains(notes, "## Unsupported-feature questions");
        foreach (var answer in safeAnswers)
            StringAssert.Contains(normalizedNotes, answer, $"Missing unsupported-feature answer '{answer}'.");

        StringAssert.Contains(normalizedNotes, "The current `search.many` execution rejects it before opening content");
        StringAssert.Contains(normalizedNotes, "It must never silently fall back to literal text");
        StringAssert.Contains(normalizedNotes, "An empty `matches`, `files` or `lines` result is not an exhaustive negative");
        StringAssert.Contains(normalizedNotes, "no Search preference, repair-rate or confidence-interval claim is made");
    }

    private static object[] SourceArguments(string method)
    {
        const string root = "./fixture";
        return method switch
        {
            "paths" => [root],
            "many" => [root, "{\"version\":1,\"patterns\":[{\"id\":\"todo\",\"pattern\":\"TODO\",\"mode\":\"literal\"}]}"],
            "bytes" => [root, "{\"version\":1,\"bytes\":\"54\"}"],
            _ => [root, "TODO"]
        };
    }

    private static string Read(params string[] parts)
    {
        return File.ReadAllText(PathFromRoot(Path.Combine(parts)));
    }

    private static JsonDocument Load(params string[] parts)
    {
        return JsonDocument.Parse(Read(parts));
    }

    private static string NormalizeWhitespace(string value)
    {
        return string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string PathFromRoot(string relativePath)
    {
        return Path.Combine(FindRepositoryRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Musoq.DataSources.sln")))
                return directory.FullName;
        }

        throw new InvalidOperationException("Could not locate the datasource repository root.");
    }
}
