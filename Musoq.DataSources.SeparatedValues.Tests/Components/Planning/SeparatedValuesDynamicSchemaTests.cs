#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.Converter.Exceptions;
using Musoq.DataSources.Structured;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Evaluator.Exceptions;
using Musoq.Parser.Diagnostics;
using Musoq.Schema.DataSources;
using Musoq.Schema.Exceptions;
using Musoq.Schema.Optimization;

using Musoq.DataSources.SeparatedValues.Components.Planning;
using Musoq.DataSources.SeparatedValues.Components.Execution;
using Musoq.DataSources.SeparatedValues.Tests.Infrastructure;

namespace Musoq.DataSources.SeparatedValues.Tests.Components.Planning;

[TestClass]
public class SeparatedValuesDynamicSchemaTests
{
    [TestMethod]
    [TestCategory("ParallaxScope")]
    [DataRow("first", "second")]
    [DataRow("c", "c")]
    public void UnionBranches_WhenReadingTheSameCsv_KeepTheirOwnProjectedColumns(string left, string right)
    {
        WithCsv("PairId,LeftBatch,RightBatch\nP1,L1,R1\n", path =>
        {
            var sql = $"""
                select {left}.PairId, {left}.LeftBatch as Batch
                from separatedvalues.comma('{QueryPath(path)}', true, 0) {left}
                union all
                select {right}.PairId, {right}.RightBatch as Batch
                from separatedvalues.comma('{QueryPath(path)}', true, 0) {right}
                order by Batch
                """;
            using var compiled = CompileWithInferenceBudget(sql);

            using var rows = compiled.Run();

            CollectionAssert.AreEqual(new[] { "P1:L1", "P1:R1" }, rows.Select(row =>
                $"{row.GetValue<string>(0)}:{row.GetValue<string>(1)}").ToArray());
        });
    }

    [TestMethod]
    [TestCategory("ParallaxScope")]
    [DataRow("trial_rows")]
    [DataRow("c")]
    public void SiblingCtes_WhenReadingDifferentCsvs_KeepTheirOwnSchemas(string trialAlias)
    {
        WithCsv("RecordType,Threshold\nP1,7\n", protocolPath =>
            WithCsv("Fold\nC\n", trialPath =>
            {
                var sql = $"""
                    WITH protocol AS (
                        SELECT Count(c.Threshold) AS Cards
                        FROM separatedvalues.comma('{QueryPath(protocolPath)}', true, 0) c
                        WHERE c.RecordType = 'P1'
                    ), trial AS (
                        SELECT Count({trialAlias}.Fold) AS Cells
                        FROM separatedvalues.comma('{QueryPath(trialPath)}', true, 0) {trialAlias}
                    )
                    SELECT p.Cards, t.Cells FROM protocol p CROSS JOIN trial t
                    """;
                using var compiled = CompileWithInferenceBudget(sql);
                using var result = compiled.Run();

                var row = result.Single();
                Assert.AreEqual((1L, 1L), (row.GetValue<long>(0), row.GetValue<long>(1)));
            }));
    }

    [TestMethod]
    [TestCategory("ParallaxScope")]
    [DataRow("typed", "raw")]
    [DataRow("j", "j")]
    public void JobMetrics_WhenCombiningACteAndCsvScan_KeepBothCounts(string validAlias, string invalidAlias)
    {
        WithCsv("State,TypedOk\nP,1\nX,0\n", path =>
        {
            var sql = $"""
                WITH typed_rows AS (
                    SELECT j.State AS State
                    FROM separatedvalues.comma('{QueryPath(path)}', true, 0) j
                    WHERE j.TypedOk::int = 1
                ), good AS (
                    SELECT Count({validAlias}.State) AS ValidCards FROM typed_rows {validAlias}
                ), bad AS (
                    SELECT Count(*) AS InvalidCards
                    FROM separatedvalues.comma('{QueryPath(path)}', true, 0) {invalidAlias}
                    WHERE {invalidAlias}.TypedOk::int <> 1
                )
                SELECT g.ValidCards, b.InvalidCards FROM good g CROSS JOIN bad b
                """;
            using var compiled = CompileWithInferenceBudget(sql);
            using var result = compiled.Run();

            var row = result.Single();
            Assert.AreEqual((1L, 1L), (row.GetValue<long>(0), row.GetValue<long>(1)));
        });
    }

    [TestMethod]
    [TestCategory("ParallaxCsvStaging")]
    public void CountDevice_WhenQuotedEmptyAppearsAfterInference_ReportsTheFailingField()
    {
        WithCsv("Device,Serial\n\"058\",\"0007\"\n\"\",\"0008\"\n", path =>
        {
            var sql = $"SELECT Count(j.Device) FROM separatedvalues.comma('{QueryPath(path)}', true, 0) j";
            using var compiled = CompileWithInferenceBudget(sql, maximumRows: 1);

            var failure = Assert.Throws<QueryExecutionException>(() =>
            {
                using var result = compiled.Run();
                _ = result.Count;
            });

            Assert.AreEqual(DiagnosticCode.MQ7011_DataSourceReadFailed, failure.Envelope?.Code);
            StringAssert.Contains(FlattenMessages(failure), "row 2 column 'Device' cannot be converted as Int64");
            Assert.IsInstanceOfType(failure.InnerException, typeof(DataSourceLifecycleException));
            var conversion = ((DataSourceLifecycleException)failure.InnerException!).InnerException!;
            Assert.AreEqual(2L, conversion.Data[SeparatedValuesFieldConversionFailure.RecordNumberKey]);
            Assert.AreEqual(1, conversion.Data[SeparatedValuesFieldConversionFailure.ColumnOrdinalKey]);
            var expectedType = (Type)conversion.Data[SeparatedValuesFieldConversionFailure.ExpectedTypeKey]!;
            Assert.AreEqual(typeof(long), Nullable.GetUnderlyingType(expectedType) ?? expectedType);
        });
    }

    [TestMethod]
    [TestCategory("ParallaxCsvStaging")]
    public void CountDevice_WhenUnquotedEmptyAppearsAfterInference_IgnoresNull()
    {
        WithCsv("Device,Serial\n\"058\",\"0007\"\n,\"0008\"\n", path =>
        {
            var sql = $"SELECT Count(j.Device) FROM separatedvalues.comma('{QueryPath(path)}', true, 0) j";
            using var compiled = CompileWithInferenceBudget(sql, maximumRows: 1);
            using var result = compiled.Run();

            Assert.AreEqual(1L, result.Single().GetValue<long>(0));
        });
    }

    [TestMethod]
    [TestCategory("ParallaxCsvStaging")]
    public void DeclaredStringColumns_WhenReadingStagedJobs_PreserveIdentifiersAndEmptyText()
    {
        WithCsv("Device,Serial\n\"058\",\"0007\"\n\"\",\"0008\"\n", path =>
        {
            var sql = $$"""
                TABLE StagedJob { Device: string, Serial: string };
                COUPLE separatedvalues.comma WITH TABLE StagedJob AS JobCsv;
                SELECT j.Device, j.Serial FROM JobCsv('{{QueryPath(path)}}', true, 0) j ORDER BY j.Serial
                """;
            using var compiled = CompileWithInferenceBudget(sql, maximumRows: 1);
            using var result = compiled.Run();

            CollectionAssert.AreEqual(
                new[] { ("058", "0007"), (string.Empty, "0008") },
                result.Select(row => (row.GetValue<string>(0), row.GetValue<string>(1))).ToArray());
        });
    }

    [TestMethod]
    public void Query_WhenColumnDoesNotExist_FailsCompilation()
    {
        WithCsv("Name\nAda\n", path =>
        {
            var exception = Assert.ThrowsExactly<MusoqQueryException>(() =>
                Compile($"select Missing from separatedvalues.comma('{QueryPath(path)}', true, 0)"));

            StringAssert.Contains(FlattenMessages(exception), "Missing");
        });
    }

    [TestMethod]
    public void Query_WhenColumnCaseDoesNotMatch_FailsCompilation()
    {
        WithCsv("Name\nAda\n", path =>
        {
            var exception = Assert.ThrowsExactly<MusoqQueryException>(() =>
                Compile($"select name from separatedvalues.comma('{QueryPath(path)}', true, 0)"));

            StringAssert.Contains(FlattenMessages(exception), "name");
        });
    }

    [TestMethod]
    public void Planner_WhenProjectionIsExplicit_BindsDenseLayoutAndExactRowCount()
    {
        WithCsv("A,B,C\n1,2,3\n4,5,6\n", path =>
        {
            var request = new SourcePlanRequest
            {
                Identity = SourceIdentity.Empty,
                RequiredColumns = [new SourceColumnRef("C")],
                SourceRuntimeSettings = new Dictionary<string, string>(),
                Predicate = null,
                OrderBy = [],
                Skip = null,
                Take = null
            };

            var result = new SeparatedValuesSchema().TryPlanSource("comma", request, path, true, 0);
            var layout = (StructuredExecutionLayout)result.ExecutionPlan.Properties![
                SeparatedValuesPlanning.LayoutPropertyName]!;

            Assert.AreEqual(1, layout.Bindings.Length);
            Assert.AreEqual("C", layout.Bindings[0].Name);
            Assert.AreEqual(2, layout.Bindings[0].SourceOrdinal);
            Assert.AreEqual(0, layout.Bindings[0].OutputOrdinal);
        });
    }

    [TestMethod]
    public void Source_WhenBoundTypeChangesBeforeExecution_FailsWithSchemaDrift()
    {
        WithCsv("Value\n1\n", path =>
        {
            var request = new SourcePlanRequest
            {
                Identity = SourceIdentity.Empty,
                RequiredColumns = [new SourceColumnRef("Value")],
                SourceRuntimeSettings = new Dictionary<string, string>(),
                Predicate = null,
                OrderBy = [],
                Skip = null,
                Take = null
            };
            var plan = new SeparatedValuesSchema()
                .TryPlanSource("comma", request, path, true, 0)
                .ExecutionPlan;
            File.WriteAllText(path, "Value\nchanged\n", new UTF8Encoding(false, true));
            var context = RuntimeV2TestContexts.CreateExecutionContext(
                CancellationToken.None,
                [new SchemaColumn("Value", 0, typeof(long))],
                executionPlan: plan);
            var source = SeparatedValuesNativeTestSource.Create<long>(path, ",", true, 0, context);

            var exception = Assert.ThrowsExactly<StructuredSchemaDriftException>(() =>
                source.Chunks.SelectMany(chunk => chunk).ToArray());

            StringAssert.Contains(FlattenMessages(exception), "changed incompatibly after binding");
        });
    }

    [TestMethod]
    public void Schema_WhenLegacyEntryPointReceivesStream_IsRejectedBeforeParameterAccess()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Name\nAda\n"));
        var context = RuntimeV2TestContexts.CreateExecutionContext();

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            NativeSourceCalls.Source(new SeparatedValuesSchema(), Schema.SourceArguments.Create(stream, Schema.SourceArguments.Create(true, Schema.SourceArguments.Create(0, Schema.SourceArguments.Empty)))).Read<object[]>("comma", context));

        StringAssert.Contains(exception.Message, "unsupported legacy row transfer");
    }

    private static CompiledQuery CompileWithInferenceBudget(string query, int? maximumRows = null)
    {
        var settings = new Dictionary<string, string>
        {
            [SeparatedValuesInferenceOptions.MaximumTimeMillisecondsSettingName] = "5000"
        };
        if (maximumRows is int rows)
            settings[SeparatedValuesInferenceOptions.MaximumRowsSettingName] = rows.ToString(CultureInfo.InvariantCulture);

        return Compile(query, settings);
    }

    private static CompiledQuery Compile(string query, IReadOnlyDictionary<string, string>? runtimeSettings = null)
    {
        return InstanceCreatorHelpers.CompileForExecution(
            query,
            $"SeparatedValuesDynamic_{Guid.NewGuid():N}",
            new CsvSchemaProvider(),
            runtimeSettings is null
                ? EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables()
                : EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables(runtimeSettings));
    }

    private static string FlattenMessages(Exception exception)
    {
        var messages = string.Empty;
        for (var current = exception; current is not null; current = current.InnerException)
            messages += current.Message + Environment.NewLine;
        return messages;
    }

    private static string QueryPath(string path)
    {
        return Path.GetFullPath(path)
            .Replace('\\', '/')
            .Replace("'", "''", StringComparison.Ordinal);
    }

    private static void WithCsv(string contents, Action<string> assertion)
    {
        var path = Path.Combine(Path.GetTempPath(), $"musoq-csv-query-{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, contents, new UTF8Encoding(false, true));

        try
        {
            assertion(path);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
