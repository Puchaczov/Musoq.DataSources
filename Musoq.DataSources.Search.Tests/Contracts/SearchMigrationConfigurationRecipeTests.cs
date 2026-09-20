#nullable enable

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;

using Musoq.DataSources.Search.Entities;

using Musoq.DataSources.Search.Sources;

using Musoq.DataSources.Search.Components.Contracts;

using Musoq.DataSources.Search.Tests.Infrastructure;

namespace Musoq.DataSources.Search.Tests.Contracts;

[TestClass]
public sealed class SearchMigrationConfigurationRecipeTests
{
    private const string MigrationPatterns =
        "array { (Id: 'deprecated', Pattern: 'OldApi()'), " +
        "(Id: 'replacement', Pattern: 'NewApi()') }";

    private const string ConfigurationPatterns =
        "array { (Id: 'config-key', Pattern: 'FeatureX') }";

    [TestMethod]
    public void MigrationRecipe_ShouldSeparateLexicalFalsePositivesAndUseSetDifference()
    {
        var root = CreateTemporaryRoot();

        try
        {
            Write(root, "src/migration.cs", "OldApi();\nNewApi();\nOldApi(); // duplicate\n");
            Write(root, "src/unmigrated.cs", "OldApi();\n");
            Write(root, "src/comments.cs", "// OldApi();\n");
            Write(root, "src/strings.cs", "var note = \"OldApi()\";\n");
            Write(root, "src/replacement-only.cs", "NewApi();\n");

            var findings = ReadFindings(root, MigrationPatterns);
            var deprecated = findings.Where(static finding => finding.Category == "deprecated").ToArray();
            var replacement = findings.Where(static finding => finding.Category == "replacement").ToArray();

            Assert.AreEqual(5, deprecated.Length);
            Assert.AreEqual(2, replacement.Length);
            Assert.AreEqual(
                2,
                deprecated.Count(static finding => finding.Path == "src/migration.cs" &&
                    finding.Evidence == "code-mention"));
            Assert.AreEqual(
                "lexical-comment",
                deprecated.Single(finding => finding.Path == "src/comments.cs").Evidence);
            Assert.AreEqual(
                "lexical-string",
                deprecated.Single(finding => finding.Path == "src/strings.cs").Evidence);

            var provenDeprecatedPaths = deprecated
                .Where(static finding => finding.Evidence == "code-mention")
                .Select(static finding => finding.Path)
                .ToHashSet(StringComparer.Ordinal);
            var provenReplacementPaths = replacement
                .Where(static finding => finding.Evidence == "code-mention")
                .Select(static finding => finding.Path)
                .ToHashSet(StringComparer.Ordinal);
            var provenUnmigrated = provenDeprecatedPaths
                .Except(provenReplacementPaths, StringComparer.Ordinal)
                .OrderBy(static path => path, StringComparer.Ordinal)
                .ToArray();

            CollectionAssert.AreEqual(
                new[] { "src/unmigrated.cs" },
                provenUnmigrated);

            CollectionAssert.AreEquivalent(
                new[] { "src/comments.cs", "src/strings.cs", "src/unmigrated.cs" },
                ReadLexicalUnmigratedPaths(root));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void ConfigurationRecipe_ShouldPreserveDuplicatesAndMissingManifestFiles()
    {
        var root = CreateTemporaryRoot();
        var manifest = new[]
        {
            "config/app.json",
            "config/legacy.json",
            "config/comments.json",
            "config/missing.json"
        };

        try
        {
            Write(root, "config/app.json", "{\"FeatureX\":true,\"FeatureX\":false}\n");
            Write(root, "config/legacy.json", "{\"Other\":true}\n");
            Write(root, "config/comments.json", "// \"FeatureX\": true\n");

            var findings = ReadFindings(root, ConfigurationPatterns)
                .Where(static finding => finding.Category == "config-key")
                .ToArray();
            var eligiblePaths = ReadPaths(root).ToHashSet(StringComparer.Ordinal);
            var status = manifest.Select(path =>
            {
                var pathFindings = findings.Where(finding => finding.Path == path).ToArray();
                var fileExists = eligiblePaths.Contains(path);
                var proven = pathFindings.Any(static finding => finding.Evidence == "configuration-key");
                return new ConfigurationStatus(
                    path,
                    pathFindings.Length > 0,
                        proven,
                        pathFindings.Length,
                        !fileExists ? "missing-file" : proven ? "proven" : "missing-key",
                    string.Join(
                        "|",
                        pathFindings
                            .Select(static finding => finding.Evidence)
                            .Distinct(StringComparer.Ordinal)));
            }).ToArray();

            CollectionAssert.AreEqual(
                new[]
                {
                    new ConfigurationStatus(
                        "config/app.json", true, true, 2, "proven", "configuration-key"),
                    new ConfigurationStatus(
                        "config/legacy.json", false, false, 0, "missing-key", string.Empty),
                    new ConfigurationStatus(
                        "config/comments.json", true, false, 1, "missing-key", "lexical-comment"),
                    new ConfigurationStatus(
                        "config/missing.json", false, false, 0, "missing-file", string.Empty)
                },
                status);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void ConfigurationRecipe_ShouldNotMakeNegativeClaimsForIncompleteScans()
    {
        var root = Path.Combine(Path.GetTempPath(), $"musoq-search-migration-missing-{Guid.NewGuid():N}");

        var rows = new SearchAuditSource(
                SearchRequest.Create(root, "FeatureX"),
                RuntimeV2TestContexts.CreateExecutionContext())
            .Chunks
            .SelectMany(static chunk => chunk)
            .ToArray();

        Assert.AreEqual(1, rows.Length);
        Assert.AreEqual(SearchOutcome.Failed, rows[0].Outcome);
        Assert.IsFalse(rows[0].Complete);
        Assert.IsFalse(rows[0].CountsExact);
        Assert.IsFalse(CanClaimNoConfigurationKey(rows[0]));
    }

    private static bool CanClaimNoConfigurationKey(SearchAudit audit)
    {
        return audit.Complete && audit.ScopeExhausted && audit.CountsExact;
    }

    private static string[] ReadLexicalUnmigratedPaths(string root)
    {
        var escapedRoot = EscapeSql(root);
        var result = Compile(
                $"select deprecated.Path " +
                $"from search.many('{escapedRoot}', {MigrationPatterns}) deprecated " +
                $"left outer join search.many('{escapedRoot}', {MigrationPatterns}) replacement " +
                "on deprecated.Path = replacement.Path and replacement.PatternId = 'replacement' " +
                "where deprecated.PatternId = 'deprecated' and replacement.Path is null " +
                "group by deprecated.Path order by deprecated.Path")
            .Run();

        return result.Rows.Select(static row => (string)row[0]).ToArray();
    }

    private static MigrationFinding[] ReadFindings(string root, string patterns)
    {
        var escapedRoot = EscapeSql(root);
        var result = Compile(
                $"select m.Path, m.PatternId, m.MatchIndex, m.LineNumber, " +
                $"m.Utf16Column, m.MatchText " +
                $"from search.many('{escapedRoot}', {patterns}) m " +
                "order by m.Path, m.PatternId, m.LineNumber, m.Utf16Column, m.MatchIndex")
            .Run();

        return result.Rows
            .Select(row =>
            {
                var path = NormalizePath((string)row[0]);
                var category = (string)row[1];
                var lineNumber = (long)row[3];
                var utf16Column = (long)row[4];
                var line = File.ReadLines(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)))
                    .ElementAt(checked((int)lineNumber - 1));
                return new MigrationFinding(
                    path,
                    category,
                    (long)row[2],
                    lineNumber,
                    utf16Column,
                    EvidenceFor(category, line, checked((int)utf16Column)),
                    (string?)row[5]);
            })
            .ToArray();
    }

    private static string[] ReadPaths(string root)
    {
        var escapedRoot = EscapeSql(root);
        var result = Compile(
                $"select p.Path from search.paths('{escapedRoot}') p order by p.Path")
            .Run();
        return result.Rows.Select(static row => NormalizePath((string)row[0])).ToArray();
    }

    private static string EvidenceFor(string category, string line, int utf16Column)
    {
        if (line.TrimStart().StartsWith("//", StringComparison.Ordinal))
            return "lexical-comment";
        if (category != "config-key" && IsInsideQuotedString(line, utf16Column))
            return "lexical-string";
        return category == "config-key" ? "configuration-key" : "code-mention";
    }

    private static bool IsInsideQuotedString(string line, int utf16Column)
    {
        var quoteCount = 0;
        var escaped = false;
        for (var index = 0; index < utf16Column; index++)
        {
            var character = line[index];
            if (character == '"' && !escaped)
                quoteCount++;
            escaped = character == '\\' && !escaped;
            if (character != '\\')
                escaped = false;
        }

        return (quoteCount & 1) == 1;
    }

    private static CompiledQuery Compile(string query)
    {
        return InstanceCreatorHelpers.CompileForExecution(
            query,
            Guid.NewGuid().ToString(),
            new SearchSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    private static string EscapeSql(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("'", "''", StringComparison.Ordinal);
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private static string CreateTemporaryRoot()
    {
        return Path.Combine(Path.GetTempPath(), $"musoq-search-migration-{Guid.NewGuid():N}");
    }

    private static void Write(string root, string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static void DeleteTemporaryRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    private sealed record MigrationFinding(
        string Path,
        string Category,
        long MatchIndex,
        long LineNumber,
        long Utf16Column,
        string Evidence,
        string? MatchText);

    private sealed record ConfigurationStatus(
        string Path,
        bool Candidate,
        bool Proven,
        int OccurrenceCount,
        string Status,
        string Evidence);
}
