#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;

using Musoq.DataSources.Search.Tests.Infrastructure;

namespace Musoq.DataSources.Search.Tests.Contracts;

[TestClass]
public sealed class SearchDiagnosticTraceabilityRecipeTests
{
    private const string TraceabilityRequest =
        "{\"version\":1,\"patterns\":[" +
        "{\"id\":\"declaration-1001\",\"pattern\":\"public const string MQ1001\",\"mode\":\"literal\"}," +
        "{\"id\":\"emission-1001\",\"pattern\":\"EmitDiagnostic(\\\"MQ1001\\\")\",\"mode\":\"literal\"}," +
        "{\"id\":\"test-1001\",\"pattern\":\"MQ1001_ShouldHaveCoverage\",\"mode\":\"literal\"}," +
        "{\"id\":\"docs-1001\",\"pattern\":\"MQ1001\",\"mode\":\"literal\"}," +
        "{\"id\":\"declaration-1002\",\"pattern\":\"public const string MQ1002\",\"mode\":\"literal\"}," +
        "{\"id\":\"emission-1002\",\"pattern\":\"EmitDiagnostic(\\\"MQ1002\\\")\",\"mode\":\"literal\"}," +
        "{\"id\":\"test-1002\",\"pattern\":\"MQ1002_ShouldHaveCoverage\",\"mode\":\"literal\"}," +
        "{\"id\":\"docs-1002\",\"pattern\":\"MQ1002\",\"mode\":\"literal\"}]}";

    [TestMethod]
    public void TraceabilityRecipe_ShouldSeparateLexicalFindingsFromProvenCoverage()
    {
        var root = CreateTemporaryRoot();

        try
        {
            Write(root, "src/diagnostics.cs", """
                public const string MQ1001 = "MQ1001";
                EmitDiagnostic("MQ1001");
                """);
            Write(root, "src/missing-test.cs", """
                EmitDiagnostic("MQ1002");
                """);
            Write(root, "src/comments.cs", """
                // public const string MQ1002 = "MQ1002";
                """);
            Write(root, "tests/diagnostics.tests.cs", """
                public void MQ1001_ShouldHaveCoverage() { AssertDiagnostic("MQ1001"); }
                """);
            Write(root, "docs/diagnostics.md", """
                MQ1001 is documented with its declaration and emitted diagnostic.
                """);

            var candidates = ReadCandidates(root);
            var expectedOccurrences = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["src/comments.cs|declaration-1002"] = 1,
                ["src/comments.cs|docs-1002"] = 2,
                ["src/diagnostics.cs|declaration-1001"] = 1,
                ["src/diagnostics.cs|docs-1001"] = 3,
                ["src/diagnostics.cs|emission-1001"] = 1,
                ["src/missing-test.cs|docs-1002"] = 1,
                ["src/missing-test.cs|emission-1002"] = 1,
                ["tests/diagnostics.tests.cs|docs-1001"] = 2,
                ["tests/diagnostics.tests.cs|test-1001"] = 1,
                ["docs/diagnostics.md|docs-1001"] = 1
            };
            var actualOccurrences = candidates
                .GroupBy(static finding => $"{finding.Code}|{finding.Category}-{finding.DiagnosticId}", StringComparer.Ordinal)
                .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal);

            Assert.AreEqual(expectedOccurrences.Values.Sum(), candidates.Length);
            Assert.AreEqual(expectedOccurrences.Count, actualOccurrences.Count);
            foreach (var expected in expectedOccurrences)
                Assert.AreEqual(expected.Value, actualOccurrences[expected.Key], expected.Key);

            Assert.IsTrue(candidates.All(static finding =>
                !string.IsNullOrWhiteSpace(finding.Code) &&
                !string.IsNullOrWhiteSpace(finding.Category) &&
                !string.IsNullOrWhiteSpace(finding.Origin) &&
                !string.IsNullOrWhiteSpace(finding.Evidence)));

            var commentFinding = candidates.Single(finding =>
                finding.Code == "src/comments.cs" &&
                finding.Category == "declaration" &&
                finding.Evidence == "lexical-comment");
            Assert.AreEqual("declaration", commentFinding.Category);
            Assert.AreEqual("code", commentFinding.Origin);

            var coverage = candidates
                .GroupBy(static finding => finding.DiagnosticId, StringComparer.Ordinal)
                .Select(group => new CoverageEvidence(
                    group.Key,
                    HasEvidence(group, "declaration", "code", excludeComments: true),
                    HasEvidence(group, "emission", "code", excludeComments: true),
                    HasEvidence(group, "test", "test", excludeComments: true),
                    HasEvidence(group, "docs", "documentation", excludeComments: true)))
                .OrderBy(static evidence => evidence.DiagnosticId, StringComparer.Ordinal)
                .ToArray();

            var complete = coverage.Single(evidence => evidence.DiagnosticId == "1001");
            Assert.IsTrue(complete.IsProven);

            var incomplete = coverage.Single(evidence => evidence.DiagnosticId == "1002");
            Assert.IsFalse(incomplete.IsProven);
            CollectionAssert.AreEquivalent(
                new[] { "declaration", "test", "docs" },
                incomplete.MissingEvidence.ToArray());
            Assert.IsTrue(candidates.Any(finding =>
                finding.DiagnosticId == "1002" &&
                finding.Category == "emission" &&
                finding.Evidence == "lexical-mention"));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    private static bool HasEvidence(
        IEnumerable<TraceabilityFinding> findings,
        string category,
        string origin,
        bool excludeComments)
    {
        return findings.Any(finding =>
            finding.Category == category &&
            finding.Origin == origin &&
            (!excludeComments || finding.Evidence != "lexical-comment"));
    }

    private static TraceabilityFinding[] ReadCandidates(string root)
    {
        var escapedRoot = EscapeSql(root);
        var escapedRequest = EscapeSql(TraceabilityRequest);
        var result = Compile(
                $"select m.Path, m.PatternId, m.LineNumber, m.Utf16Column, m.MatchText " +
                $"from search.many('{escapedRoot}', '{escapedRequest}') m " +
                "order by m.Path, m.PatternId, m.LineNumber, m.Utf16Column")
            .Run();

        return result.Rows
            .Select(row =>
            {
                var path = NormalizePath((string)row[0]);
                var patternId = (string)row[1];
                var parts = patternId.Split('-', 2, StringSplitOptions.None);
                var category = parts[0];
                var diagnosticId = parts.Length == 2 ? parts[1] : string.Empty;
                var origin = OriginFor(path);
                var line = File.ReadLines(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)))
                    .ElementAt(checked((int)(long)row[2] - 1));
                var evidence = line.TrimStart().StartsWith("//", StringComparison.Ordinal)
                    ? "lexical-comment"
                    : "lexical-mention";
                return new TraceabilityFinding(
                    path,
                    category,
                    diagnosticId,
                    origin,
                    evidence,
                    (string?)row[4],
                    (long)row[2],
                    (long)row[3]);
            })
            .ToArray();
    }

    private static string OriginFor(string path)
    {
        return path switch
        {
            _ when path.StartsWith("src/", StringComparison.Ordinal) => "code",
            _ when path.StartsWith("tests/", StringComparison.Ordinal) => "test",
            _ when path.StartsWith("docs/", StringComparison.Ordinal) => "documentation",
            _ => "other"
        };
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
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

    private static string CreateTemporaryRoot()
    {
        return Path.Combine(Path.GetTempPath(), $"musoq-search-traceability-{Guid.NewGuid():N}");
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

    private sealed record TraceabilityFinding(
        string Code,
        string Category,
        string DiagnosticId,
        string Origin,
        string Evidence,
        string? MatchText,
        long LineNumber,
        long Utf16Column);

    private sealed record CoverageEvidence(
        string DiagnosticId,
        bool HasDeclaration,
        bool HasEmission,
        bool HasTest,
        bool HasDocs)
    {
        public bool IsProven => HasDeclaration && HasEmission && HasTest && HasDocs;

        public IReadOnlyList<string> MissingEvidence =>
        [
            .. (!HasDeclaration ? ["declaration"] : Array.Empty<string>()),
            .. (!HasEmission ? ["emission"] : Array.Empty<string>()),
            .. (!HasTest ? ["test"] : Array.Empty<string>()),
            .. (!HasDocs ? ["docs"] : Array.Empty<string>())
        ];
    }
}
