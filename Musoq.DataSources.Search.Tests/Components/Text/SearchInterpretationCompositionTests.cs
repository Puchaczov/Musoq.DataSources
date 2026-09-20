#nullable enable

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.DataSources.Search.Tests.Infrastructure;

namespace Musoq.DataSources.Search.Tests.Components.Text;

[TestClass]
public sealed class SearchInterpretationCompositionTests
{
    [TestMethod]
    public void SearchLines_Parse_ShouldParseOneRowPerMatchingLineAndRetainOrigin()
    {
        var root = CreateTemporaryRoot();
        const string content = "2026-09-07 INFO: booted\n2026-09-07 ERROR: first ERROR second";

        try
        {
            WriteFixture(root, content);
            using var compiled = Compile(BuildQuery(root, "Parse<LogEntry>(candidates.LineText) log"));

            var result = compiled.Run();
            Assert.AreEqual(1, result.Count);

            var actualColumns = result.Columns.Select(column => column.ColumnName).ToArray();
            CollectionAssert.AreEqual(
                new[]
                {
                    "candidates.Path", "candidates.Origin", "candidates.LineNumber", "candidates.OccurrenceCount",
                    "log.Timestamp", "log.Level", "log.Message"
                },
                actualColumns);
            Assert.AreEqual("fixture.log", result[0][0]);
            Assert.IsNull(result[0][1]);
            Assert.AreEqual(2L, result[0][2]);
            Assert.AreEqual(2L, result[0][3]);
            Assert.AreEqual("2026-09-07", result[0][4]);
            Assert.AreEqual("ERROR", result[0][5]);
            Assert.AreEqual("first ERROR second", result[0][6]);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void SearchLines_Parse_ShouldFailForMalformedRecord()
    {
        var root = CreateTemporaryRoot();

        try
        {
            WriteFixture(root, "2026-09-07 ERROR missing-separator");
            using var compiled = Compile(BuildQuery(root, "Parse<LogEntry>(candidates.LineText) log"));

            Assert.ThrowsException<Musoq.Schema.Interpreters.ParseException>(() => _ = compiled.Run().Count);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void SearchLines_TryParse_ShouldPreserveMalformedCandidateAsNull()
    {
        var root = CreateTemporaryRoot();

        try
        {
            WriteFixture(root, "2026-09-07 ERROR missing-separator");
            using var compiled = Compile(
                BuildQuery(root, "TryParse<LogEntry>(candidates.LineText) log", outerApply: true));

            var result = compiled.Run();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("fixture.log", result[0][0]);
            Assert.IsNull(result[0][1]);
            Assert.AreEqual(1L, result[0][2]);
            Assert.AreEqual(1L, result[0][3]);
            Assert.IsNull(result[0][4]);
            Assert.IsNull(result[0][5]);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void SearchLines_PartialParse_ShouldExposeMalformedFieldAndSourceIdentity()
    {
        var root = CreateTemporaryRoot();

        try
        {
            WriteFixture(root, "2026-09-07 ERROR missing-separator");
            using var compiled = Compile(
                BuildQuery(root, "PartialParse<LogEntry>(candidates.LineText) parsed"));

            var result = compiled.Run();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("fixture.log", result[0][0]);
            Assert.IsNull(result[0][1]);
            Assert.AreEqual(1L, result[0][2]);
            Assert.AreEqual(1L, result[0][3]);
            Assert.AreEqual("Level", result[0][4]);
            StringAssert.Contains((string)result[0][5]!, "ISE");
            Assert.IsTrue((int)result[0][6]! > 0);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void SearchLines_ShouldMarkAnUnmatchedValidRecordAsCandidateOnly()
    {
        var root = CreateTemporaryRoot();

        try
        {
            WriteFixture(root, "2026-09-07 INFO: valid-without-error");
            using var compiled = Compile(BuildQuery(root, "Parse<LogEntry>(candidates.LineText) log"));

            var result = compiled.Run();

            Assert.AreEqual(0, result.Count);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    private static string BuildQuery(
        string root,
        string interpretation,
        bool outerApply = false)
    {
        var escapedRoot = root.Replace("\\", "\\\\", StringComparison.Ordinal);
        var apply = outerApply ? "outer apply" : "cross apply";
        return $@"
            text LogEntry {{
                Timestamp: until ' ',
                Level: until ':',
                Separator: literal ' ',
                Message: rest trim
            }};
            with candidates as (
                select line.Path, line.Origin, line.LineNumber, line.OccurrenceCount, line.LineText
                from search.lines('{escapedRoot}', 'ERROR') line
            )
            select
                candidates.Path,
                candidates.Origin,
                candidates.LineNumber,
                candidates.OccurrenceCount,
                {(interpretation.StartsWith("PartialParse", StringComparison.Ordinal)
                    ? "parsed.ErrorField, parsed.ErrorMessage, parsed.BytesConsumed"
                    : "log.Timestamp, log.Level, log.Message" )}
            from candidates
            {apply} {interpretation}";
    }

    private static CompiledQuery Compile(string query)
    {
        return InstanceCreatorHelpers.CompileForExecution(
            query,
            Guid.NewGuid().ToString(),
            new SearchSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    private static string CreateTemporaryRoot()
    {
        return Path.Combine(Path.GetTempPath(), $"musoq-search-interpretation-{Guid.NewGuid():N}");
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
}
