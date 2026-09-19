#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.RepresentativeTests.Components;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;

namespace Musoq.DataSources.RepresentativeTests;

[TestClass]
public sealed class SearchRepresentativeRecipeTests
{
    private const string TraceabilityPatterns =
        "array { " +
        "(Id: 'declaration-1001', Pattern: 'public const string MQ1001'), " +
        "(Id: 'emission-1001', Pattern: 'EmitDiagnostic(\"MQ1001\")') }";

    private const string ProximityPatterns =
        "array { " +
        "(Id: 'left', Pattern: 'LEFT'), " +
        "(Id: 'right', Pattern: 'RIGHT') }";

    private const string MigrationPatterns =
        "array { " +
        "(Id: 'deprecated', Pattern: 'OldApi()'), " +
        "(Id: 'replacement', Pattern: 'NewApi()') }";

    private const string ConfigurationPatterns =
        "array { (Id: 'config-key', Pattern: 'FeatureX') }";

    [TestMethod]
    public void SearchDiagnosticTraceabilityRecipe_ShouldRunCompiledQueryAndRetainFailureExample()
    {
        var root = CreateTemporaryRoot("traceability");

        try
        {
            Write(root, "src/diagnostics.cs", "public const string MQ1001 = \"MQ1001\";\nEmitDiagnostic(\"MQ1001\");\n");
            Write(root, "src/missing-emission.cs", "public const string MQ1001 = \"MQ1001\";\n");

            var result = Compile(
                    "select m.Path, m.PatternId, m.MatchText " +
                    $"from search.many('{EscapeSql(root)}', {TraceabilityPatterns}) m " +
                    "order by m.Path, m.PatternId, m.MatchIndex")
                .Run();

            Assert.AreEqual(3, result.Count);
            CollectionAssert.AreEqual(
                new[]
                {
                    "src/diagnostics.cs|declaration-1001|public const string MQ1001",
                    "src/diagnostics.cs|emission-1001|EmitDiagnostic(\"MQ1001\")",
                    "src/missing-emission.cs|declaration-1001|public const string MQ1001"
                },
                result.Rows.Select(row => $"{NormalizePath((string)row[0])}|{row[1]}|{row[2]}").ToArray());
            Assert.IsFalse(result.Rows.Any(row =>
                NormalizePath((string)row[0]) == "src/missing-emission.cs" &&
                (string)row[1] == "emission-1001"));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void SearchProximityRecipe_ShouldRunBoundedSameFilePairsAndRejectBoundaryMatch()
    {
        var root = CreateTemporaryRoot("proximity");

        try
        {
            Write(root, "alpha.log", "LEFT RIGHT\nLEFT\nRIGHT\n");
            Write(root, "left-only.log", "LEFT\n");
            Write(root, "right-only.log", "RIGHT\n");

            var result = Compile($@"
                with occurrences as (
                    select m.Path, m.PatternId, m.LineNumber, m.Utf16Column, m.Utf16Length
                    from search.many('{EscapeSql(root)}', {ProximityPatterns}) m
                ), pairs as (
                    select lefts.Path,
                           lefts.LineNumber as LeftLine,
                           rights.LineNumber as RightLine,
                           case when rights.LineNumber = lefts.LineNumber
                                then rights.Utf16Column - (lefts.Utf16Column + lefts.Utf16Length)
                                else null end as SameLineGap
                    from occurrences lefts
                    inner join occurrences rights
                        on lefts.Path = rights.Path
                        and lefts.PatternId = 'left'
                        and rights.PatternId = 'right'
                        and rights.LineNumber >= lefts.LineNumber
                        and rights.LineNumber <= lefts.LineNumber + 2
                        and (rights.LineNumber > lefts.LineNumber or
                             rights.Utf16Column >= lefts.Utf16Column + lefts.Utf16Length)
                )
                select Path, LeftLine, RightLine, SameLineGap
                from pairs
                order by Path, LeftLine, RightLine")
                .Run();

            Assert.AreEqual(3, result.Count);
            CollectionAssert.AreEqual(
                new[]
                {
                    "alpha.log|1|1|1",
                    "alpha.log|1|3|",
                    "alpha.log|2|3|"
                },
                result.Rows.Select(row =>
                    $"{NormalizePath((string)row[0])}|{row[1]}|{row[2]}|{row[3]}").ToArray());
            Assert.IsFalse(result.Rows.Any(row => NormalizePath((string)row[0]) != "alpha.log"));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void SearchMigrationAndConfigurationRecipes_ShouldRunCandidatesAndPreserveFailureBoundaries()
    {
        var root = CreateTemporaryRoot("migration-configuration");

        try
        {
            Write(root, "src/legacy.cs", "OldApi();\n");
            Write(root, "src/migrated.cs", "OldApi();\nNewApi();\n");
            Write(root, "src/comments.cs", "// OldApi();\n");
            Write(root, "src/replacement-only.cs", "NewApi();\n");
            Write(root, "config/app.json", "{\"FeatureX\":true,\"FeatureX\":false}\n");
            Write(root, "config/legacy.json", "{\"Other\":true}\n");

            var migration = Compile(
                    "with findings as (" +
                    $"select m.Path, m.PatternId, m.MatchText from search.many('{EscapeSql(root)}', {MigrationPatterns}) m" +
                    ") " +
                    "select deprecated.Path, deprecated.MatchText " +
                    "from findings deprecated " +
                    "left outer join findings replacement " +
                    "on deprecated.Path = replacement.Path and replacement.PatternId = 'replacement' " +
                    "where deprecated.PatternId = 'deprecated' and replacement.Path is null " +
                    "group by deprecated.Path, deprecated.MatchText " +
                    "order by deprecated.Path")
                .Run();

            Assert.AreEqual(2, migration.Count);
            CollectionAssert.AreEqual(
                new[] { "src/comments.cs|OldApi()", "src/legacy.cs|OldApi()" },
                migration.Rows.Select(row => $"{NormalizePath((string)row[0])}|{row[1]}").ToArray());

            var configuration = Compile(
                    "select m.Path, m.MatchIndex, m.MatchText " +
                    $"from search.many('{EscapeSql(root)}', {ConfigurationPatterns}) m " +
                    "order by m.Path, m.MatchIndex")
                .Run();

            Assert.AreEqual(2, configuration.Count);
            Assert.IsTrue(configuration.Rows.All(row => NormalizePath((string)row[0]) == "config/app.json"));
            CollectionAssert.AreEqual(new[] { 0L, 1L }, configuration.Rows.Select(row => (long)row[1]).ToArray());

            var paths = Compile($"select p.Path from search.paths('{EscapeSql(root)}') p order by p.Path").Run();
            Assert.IsFalse(paths.Rows.Any(row => NormalizePath((string)row[0]) == "config/missing.json"));
            Assert.IsTrue(paths.Rows.Any(row => NormalizePath((string)row[0]) == "config/legacy.json"));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void SearchLogToParseRecipe_ShouldRunTolerantParseAndRetainMalformedCandidate()
    {
        var root = CreateTemporaryRoot("log-parse");

        try
        {
            Write(root, "service.log",
                "evt-1 2026-09-07 ERROR: first ERROR second\n" +
                "evt-2 2026-09-07 ERROR: café 🚀 ERROR\n" +
                "evt-3 2026-09-07 ERROR missing-separator\n" +
                "evt-4 2026-09-07 INFO: missing-error-token\n");

            var result = Compile($@"
                text LogRecord {{
                    EventId: until ' ',
                    Timestamp: until ' ',
                    Level: until ':',
                    Separator: literal ' ',
                    Message: rest trim
                }};
                with candidates as (
                    select line.Path, line.LineNumber, line.OccurrenceCount, line.LineText
                    from search.lines('{EscapeSql(root)}', 'ERROR') line
                )
                select candidates.Path,
                       candidates.LineNumber,
                       candidates.OccurrenceCount,
                       candidates.LineText,
                       log.EventId,
                       log.Message
                from candidates
                outer apply TryParse<LogRecord>(candidates.LineText) log
                order by candidates.LineNumber")
                .Run();

            Assert.AreEqual(3, result.Count);
            Assert.AreEqual(5L, result.Rows.Sum(row => (long)row[2]));
            Assert.AreEqual("evt-1", result.Rows[0][4]);
            Assert.AreEqual("café 🚀 ERROR", result.Rows[1][5]);
            Assert.IsNull(result.Rows[2][4]);
            StringAssert.Contains((string)result.Rows[2][3], "missing-separator");
            Assert.IsFalse(result.Rows.Any(row => (long)row[1] == 4));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    private static CompiledQuery Compile(string query)
    {
        return InstanceCreatorHelpers.CompileForExecution(
            query,
            Guid.NewGuid().ToString(),
            new RepresentativeSchemaProvider(),
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

    private static string CreateTemporaryRoot(string recipe)
    {
        return Path.Combine(Path.GetTempPath(), $"musoq-search-representative-{recipe}-{Guid.NewGuid():N}");
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
}
