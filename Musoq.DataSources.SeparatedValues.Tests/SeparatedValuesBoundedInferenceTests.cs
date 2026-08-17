#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.DataSources.Structured;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues.Tests;

[TestClass]
public class SeparatedValuesBoundedInferenceTests
{
    [TestMethod]
    public void InferenceOptions_DefaultToOneMiB4096RowsAndTenMilliseconds()
    {
        var options = SeparatedValuesInferenceOptions.From(new Dictionary<string, string>());

        Assert.AreEqual(1024L * 1024L, options.MaximumBytes);
        Assert.AreEqual(4096, options.MaximumRows);
        Assert.AreEqual(TimeSpan.FromMilliseconds(10), options.MaximumTime);
    }

    [TestMethod]
    public void DynamicResolution_StopsAtConfiguredRowLimit()
    {
        WithCsv("Value\n1\n2\n3\n4\n", path =>
        {
            var plan = Plan(path, null, MaximumRows(2));
            var contract = SeparatedValuesSourceContract.From(plan);

            Assert.AreEqual(SeparatedValuesSchemaResolutionMode.Sampled, contract.Mode);
            Assert.AreEqual(2L, contract.InspectedRows);
            Assert.IsFalse(contract.HasExactCardinality);
            Assert.IsTrue(contract.InspectedBytes <= SeparatedValuesInferenceOptions.DefaultMaximumBytes);
            Assert.AreEqual(typeof(long?), contract.Snapshot.Columns.Single().ClrType);
        });
    }

    [TestMethod]
    public void DynamicResolution_WhenCompleteFirstRecordExceedsByteBudget_FailsClearly()
    {
        var contents = new string('H', 1024 * 1024) + "\n1\n";
        WithCsv(contents, path =>
        {
            var settings = MaximumRows(4096);
            settings[SeparatedValuesInferenceOptions.MaximumBytesSettingName] = (128 * 1024 + 8).ToString();

            var exception = Assert.ThrowsExactly<InvalidDataException>(() => Plan(path, null, settings));

            StringAssert.Contains(exception.Message, "bounded budget");
            StringAssert.Contains(exception.Message, SeparatedValuesInferenceOptions.MaximumBytesSettingName);
        });
    }

    [TestMethod]
    public void InferenceReader_WhenDeadlineHasElapsed_DoesNotIssueAnotherRead()
    {
        WithCsv("Value\n1\n", path =>
        {
            using var reader = new SeparatedValuesUtf8Reader(
                path,
                (byte)',',
                0,
                64 * 1024,
                SeparatedValuesInferenceOptions.DefaultMaximumBytes,
                Stopwatch.GetTimestamp() - 1,
                CancellationToken.None);

            reader.Prepare();

            Assert.IsTrue(reader.BudgetExhausted);
            Assert.AreEqual(0L, reader.BytesRead);
            Assert.IsFalse(reader.TryRead(out _));
        });
    }

    [TestMethod]
    public void ConcreteColumns_AreAuthoritativeWithoutSamplingDataRows()
    {
        WithCsv("When,Payload\nnot-a-date,ignored\n", path =>
        {
            var resolver = new BoundedSeparatedValuesSchemaResolver();
            ISchemaColumn[] declared = [new SchemaColumn("When", 0, typeof(DateTime))];

            var contract = resolver.Resolve(new SeparatedValuesSchemaResolutionRequest(
                path,
                ",",
                true,
                0,
                declared,
                GenerousSettings(),
                CancellationToken.None));

            Assert.AreEqual(SeparatedValuesSchemaResolutionMode.Declared, contract.Mode);
            Assert.AreEqual(0L, contract.InspectedRows);
            Assert.IsFalse(contract.HasExactCardinality);
            Assert.AreEqual(2, contract.Snapshot.Columns.Length);
            Assert.AreEqual(typeof(DateTime), contract.ColumnTypes[0]);
            Assert.AreEqual(typeof(string), contract.ColumnTypes[1]);
        });
    }

    [TestMethod]
    public void SampledTypeDrift_FailsWithExactRowAndColumn()
    {
        WithCsv("Value\n1\n2\nnot-a-number\n", path =>
        {
            var plan = Plan(path, null, MaximumRows(1));
            var context = RuntimeV2TestContexts.CreateExecutionContext(
                CancellationToken.None,
                [new SchemaColumn("Value", 0, typeof(long?))],
                executionPlan: plan);
            var source = new SeparatedValuesFromFileRowsSource(path, ",", true, 0, context);

            var exception = Assert.ThrowsExactly<FormatException>(() =>
                source.Chunks.SelectMany(chunk => chunk).ToArray());

            StringAssert.Contains(exception.Message, "row 3");
            StringAssert.Contains(exception.Message, "column 'Value'");
            StringAssert.Contains(exception.Message, "not-a-number");
        });
    }

    [TestMethod]
    public void HeaderlessWidthDrift_AfterTheSample_FailsExplicitly()
    {
        WithCsv("1,2\n3,4,5\n", path =>
        {
            var settings = MaximumRows(1);
            var request = new SourcePlanRequest
            {
                Identity = SourceIdentity.Empty,
                RequiredColumns = [new SourceColumnRef("Column1")],
                SourceRuntimeSettings = settings,
                Predicate = null,
                OrderBy = [],
                Skip = null,
                Take = null
            };
            var plan = new SeparatedValuesSchema()
                .TryPlanSource("comma", request, path, false, 0)
                .ExecutionPlan;
            var context = RuntimeV2TestContexts.CreateExecutionContext(
                CancellationToken.None,
                [new SchemaColumn("Column1", 0, typeof(long?))],
                executionPlan: plan);

            var exception = Assert.ThrowsExactly<StructuredSchemaDriftException>(() =>
                new SeparatedValuesFromFileRowsSource(path, ",", false, 0, context)
                    .Chunks
                    .SelectMany(chunk => chunk)
                    .ToArray());

            StringAssert.Contains(exception.Message, "row 2");
            StringAssert.Contains(exception.Message, "more than the bound 2 columns");
        });
    }

    [TestMethod]
    public void DeclaredContract_WithTake_DoesNotValidateUnconsumedTail()
    {
        WithCsv("Value\n1\nnot-a-number\n", path =>
        {
            var schema = new SeparatedValuesSchema();
            var metadata = new SourceMetadataContext(
                "declared-take",
                CancellationToken.None,
                [new SchemaColumn("Value", 0, typeof(long))],
                GenerousSettings(),
                new Mock<ILogger>().Object);
            _ = schema.DescribeSource(
                "comma",
                new SourceDescribeContext(SourceIdentity.Empty, metadata),
                path,
                true,
                0);
            var plan = schema.TryPlanSource(
                    "comma",
                    Request(1, GenerousSettings()),
                    path,
                    true,
                    0)
                .ExecutionPlan;
            var context = RuntimeV2TestContexts.CreateExecutionContext(
                CancellationToken.None,
                [new SchemaColumn("Value", 0, typeof(long))],
                executionPlan: plan);

            var rows = new SeparatedValuesFromFileRowsSource(path, ",", true, 0, context)
                .Chunks
                .SelectMany(chunk => chunk)
                .ToArray();

            Assert.AreEqual(1, rows.Length);
            Assert.AreEqual(1L, rows[0][0]);
        });
    }

    [TestMethod]
    public void DeclaredContract_WithTake_DoesNotParseMalformedUnconsumedRecord()
    {
        WithCsv("Value\nfirst\nbad\"quote\n", path =>
        {
            var schema = new SeparatedValuesSchema();
            var metadata = new SourceMetadataContext(
                "declared-grammar-take",
                CancellationToken.None,
                [new SchemaColumn("Value", 0, typeof(string))],
                GenerousSettings(),
                new Mock<ILogger>().Object);
            _ = schema.DescribeSource(
                "comma",
                new SourceDescribeContext(SourceIdentity.Empty, metadata),
                path,
                true,
                0);
            var plan = schema.TryPlanSource(
                    "comma",
                    Request(1, GenerousSettings()),
                    path,
                    true,
                    0)
                .ExecutionPlan;
            var context = RuntimeV2TestContexts.CreateExecutionContext(
                CancellationToken.None,
                [new SchemaColumn("Value", 0, typeof(string))],
                executionPlan: plan);

            var rows = new SeparatedValuesFromFileRowsSource(path, ",", true, 0, context)
                .Chunks
                .SelectMany(chunk => chunk)
                .ToArray();

            Assert.AreEqual(1, rows.Length);
            Assert.AreEqual("first", rows[0][0]);
        });
    }

    private static SourceExecutionPlan Plan(
        string path,
        long? take,
        IReadOnlyDictionary<string, string> settings)
    {
        return new SeparatedValuesSchema()
            .TryPlanSource("comma", Request(take, settings), path, true, 0)
            .ExecutionPlan;
    }

    private static SourcePlanRequest Request(
        long? take,
        IReadOnlyDictionary<string, string> settings)
    {
        return new SourcePlanRequest
        {
            Identity = SourceIdentity.Empty,
            RequiredColumns = [new SourceColumnRef("Value")],
            SourceRuntimeSettings = settings,
            Predicate = null,
            OrderBy = [],
            Skip = null,
            Take = take
        };
    }

    private static Dictionary<string, string> MaximumRows(int rows)
    {
        var settings = GenerousSettings();
        settings[SeparatedValuesInferenceOptions.MaximumRowsSettingName] = rows.ToString();
        return settings;
    }

    private static Dictionary<string, string> GenerousSettings()
    {
        return new Dictionary<string, string>
        {
            [SeparatedValuesInferenceOptions.MaximumBytesSettingName] =
                SeparatedValuesInferenceOptions.DefaultMaximumBytes.ToString(),
            [SeparatedValuesInferenceOptions.MaximumRowsSettingName] =
                SeparatedValuesInferenceOptions.DefaultMaximumRows.ToString(),
            [SeparatedValuesInferenceOptions.MaximumTimeMillisecondsSettingName] = "1000"
        };
    }

    private static void WithCsv(string contents, Action<string> assertion)
    {
        var path = Path.Combine(Path.GetTempPath(), $"musoq-csv-bounded-{Guid.NewGuid():N}.csv");
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
