#nullable enable

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;

using Musoq.DataSources.Search.Tests.Infrastructure;

namespace Musoq.DataSources.Search.Tests.Contracts;

[TestClass]
public sealed class SearchLogSearchToParseRecipeTests
{
    [TestMethod]
    public void LogRecipe_ShouldParseOncePerCandidateLineAndAggregateFailuresAndIds()
    {
        var root = CreateTemporaryRoot();

        try
        {
            WriteFixture(
                root,
                "evt-1 2026-09-07 ERROR: first ERROR second\n" +
                "evt-2 2026-09-07 ERROR: café 🚀 ERROR\n" +
                "evt-3 2026-09-07 ERROR missing-separator\n" +
                "evt-4 2026-09-07 INFO: missing-error-token\n");

            var parsed = ReadParsedRows(root);
            Assert.AreEqual(3, parsed.Length);
            Assert.AreEqual(5L, parsed.Sum(static row => row.OccurrenceCount));
            Assert.AreEqual(3, parsed.Select(static row => row.LineNumber).Distinct().Count());

            var successful = parsed.Where(static row => !row.ParseFailed).ToArray();
            var ids = successful
                .Select(static row => row.EventId!)
                .OrderBy(static id => id, StringComparer.Ordinal)
                .ToArray();
            CollectionAssert.AreEqual(new[] { "evt-1", "evt-2" }, ids);
            Assert.IsTrue(successful.All(static row => row.EventId is not null && row.Message is not null));
            Assert.AreEqual("café 🚀 ERROR", successful.Single(row => row.EventId == "evt-2").Message);
            Assert.AreEqual(2L, parsed.Single(row => row.LineNumber == 1).OccurrenceCount);
            Assert.AreEqual(2L, parsed.Single(row => row.LineNumber == 2).OccurrenceCount);

            var failures = parsed.Where(static row => row.ParseFailed).ToArray();
            Assert.AreEqual(1, failures.Length);
            Assert.AreEqual(3L, failures[0].LineNumber);
            Assert.AreEqual(1L, failures[0].OccurrenceCount);
            Assert.IsNull(failures[0].EventId);
            StringAssert.Contains(failures[0].LineText!, "missing-separator");
            Assert.IsFalse(parsed.Any(static row => row.LineNumber == 4));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void LogRecipe_ShouldRetainUtf16CoordinatesForMultibyteCandidateLines()
    {
        var root = CreateTemporaryRoot();

        try
        {
            WriteFixture(root, "evt-2 2026-09-07 ERROR: café 🚀 ERROR\n");
            var result = Compile(
                    $"select m.Path, m.MatchIndex, m.LineNumber, m.Utf16Column, " +
                    $"m.Utf16Length, m.MatchText from search.matches('{EscapeSql(root)}', 'ERROR') m " +
                    "order by m.MatchIndex")
                .Run();

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(1L, result.Rows[0][2]);
            Assert.AreEqual(17L, result.Rows[0][3]);
            Assert.AreEqual(5L, result.Rows[0][4]);
            Assert.AreEqual(32L, result.Rows[1][3]);
            Assert.AreEqual("ERROR", result.Rows[1][5]);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    private static ParsedLogRow[] ReadParsedRows(string root)
    {
        var query = $@"
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
                   log.Timestamp,
                   log.Level,
                   log.Message
            from candidates
            outer apply TryParse<LogRecord>(candidates.LineText) log
            order by candidates.LineNumber";

        var result = Compile(query).Run();
        return result.Rows
            .Select(row => new ParsedLogRow(
                (string)row[0],
                (long)row[1],
                (long)row[2],
                (string?)row[3],
                (string?)row[4],
                (string?)row[5],
                (string?)row[6],
                (string?)row[7]))
            .ToArray();
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
        return Path.Combine(Path.GetTempPath(), $"musoq-search-log-recipe-{Guid.NewGuid():N}");
    }

    private static void WriteFixture(string root, string content)
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "fixture.log"), content);
    }

    private static void DeleteTemporaryRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    private sealed record ParsedLogRow(
        string Path,
        long LineNumber,
        long OccurrenceCount,
        string? LineText,
        string? EventId,
        string? Timestamp,
        string? Level,
        string? Message)
    {
        public bool ParseFailed => EventId is null;
    }
}
