#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Structured;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Json.Tests;

[TestClass]
public class JsonParallelExecutionTests
{
    [TestMethod]
    public void JsonSource_WhenParallelismIsForced_MatchesSequentialRowsAndOrder()
    {
        WithGeneratedJson(20_000, path =>
        {
            var predicate = GreaterThanOrEqual("Group", 5L);
            SourceColumnRef[] required = [new("Index"), new("Text")];
            var plan = Plan(path, predicate, required);

            var sequential = ReadRows(path, plan, MaximumParallelism("1"));
            var parallel = ReadRows(path, plan, MaximumParallelism("4"));

            Assert.AreEqual(sequential.Length, parallel.Length);
            Assert.AreEqual(10_000, parallel.Length);
            for (var index = 0; index < parallel.Length; index++)
                CollectionAssert.AreEqual(sequential[index], parallel[index]);
            Assert.AreEqual(5L, parallel[0][0]);
            Assert.AreEqual(19_999L, parallel[^1][0]);
        });
    }

    [TestMethod]
    public void JsonSource_WhenParallelismIsForced_ReportsIdenticalProgress()
    {
        WithGeneratedJson(20_000, path =>
        {
            SourceColumnRef[] required = [new("Index")];
            var plan = Plan(path, GreaterThanOrEqual("Group", 8L), required);
            var capture = new DataSourceProgressCapture();
            var context = Context(plan, MaximumParallelism("4"), capture.Handler);

            var rows = new JsonSource(path, context).Chunks.SelectMany(chunk => chunk).ToArray();

            Assert.AreEqual(4_000, rows.Length);
            Assert.AreEqual(20_000L, capture.For("json", DataSourcePhase.RowsKnown).Single().TotalRows);
            Assert.AreEqual(20_000L, capture.For("json", DataSourcePhase.RowsRead).Last().RowsProcessed);
            Assert.AreEqual(20_000L, capture.For("json", DataSourcePhase.End).Single().RowsProcessed);
        });
    }

    [TestMethod]
    public void JsonSource_WhenParallelWorkerConversionFails_PropagatesOriginalError()
    {
        var path = TempPath();
        try
        {
            using (var stream = File.Create(path))
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartArray();
                for (var row = 0; row < 20_000; row++)
                {
                    writer.WriteStartObject();
                    writer.WriteString("Value", row.ToString());
                    writer.WriteEndObject();
                }

                writer.WriteStartObject();
                writer.WriteString("Value", "not-a-number");
                writer.WriteEndObject();
                writer.WriteEndArray();
            }

            SourceColumnRef[] required = [new("Value")];
            var plan = Plan(path, null, required);
            var context = RuntimeV2TestContexts.CreateExecutionContext(
                CancellationToken.None,
                [new SchemaColumn("Value", 0, typeof(long))],
                MaximumParallelism("4"),
                executionPlan: plan);
            var source = new JsonSource(path, context);

            var exception = Assert.ThrowsExactly<FormatException>(() =>
                source.Chunks.SelectMany(chunk => chunk).ToArray());

            StringAssert.Contains(FlattenMessages(exception), "Value");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void JsonSource_WhenParallelScanIsCancelled_StopsWorkersWithoutDeadlock()
    {
        WithGeneratedJson(100_000, path =>
        {
            using var cancellation = new CancellationTokenSource();
            var capture = new DataSourceProgressCapture();
            var record = capture.Handler;
            DataSourceEventHandler callback = (sender, args) =>
            {
                record(sender, args);
                if (args.Phase == DataSourcePhase.RowsRead)
                    cancellation.Cancel();
            };
            SourceColumnRef[] required = [new("Index")];
            var plan = Plan(path, null, required);
            var context = RuntimeV2TestContexts.CreateExecutionContext(
                cancellation.Token,
                [new SchemaColumn("Index", 0, typeof(long))],
                MaximumParallelism("4"),
                dataSourceProgressCallback: callback,
                executionPlan: plan);
            var source = new JsonSource(path, context);

            Assert.ThrowsExactly<OperationCanceledException>(() =>
                source.Chunks.SelectMany(chunk => chunk).ToArray());
        });
    }

    [TestMethod]
    public void ParallelOptions_WhenSliceIsAccepted_ForceSequentialExecution()
    {
        WithGeneratedJson(1_000, path =>
        {
            var plan = Plan(path, null, [new SourceColumnRef("Index")], take: 10);
            var snapshot = JsonSchemaDiscovery.GetSnapshot(path);
            var context = Context(plan, MaximumParallelism("8"));

            Assert.AreEqual(1, JsonParallelScanOptions.Resolve(snapshot, context));
        });
    }

    [TestMethod]
    public void ParallelOptions_WhenRootIsOneObject_UseSequentialExecution()
    {
        var path = TempPath();
        File.WriteAllText(path, "{\"Index\":1,\"Text\":\"one\"}", new UTF8Encoding(false, true));
        try
        {
            var plan = Plan(path, null, [new SourceColumnRef("Index")]);
            var snapshot = JsonSchemaDiscovery.GetSnapshot(path);
            var context = Context(plan, MaximumParallelism("8"));

            Assert.AreEqual(1, JsonParallelScanOptions.Resolve(snapshot, context));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void ParallelOptions_WhenSettingIsInvalid_ThrowsClearError()
    {
        WithGeneratedJson(100, path =>
        {
            var plan = Plan(path, null, [new SourceColumnRef("Index")]);
            var snapshot = JsonSchemaDiscovery.GetSnapshot(path);
            var context = Context(plan, MaximumParallelism("many"));

            var exception = Assert.ThrowsExactly<ArgumentException>(() =>
                JsonParallelScanOptions.Resolve(snapshot, context));

            StringAssert.Contains(exception.Message, JsonParallelScanOptions.MaximumParallelismSettingName);
        });
    }

    [TestMethod]
    public void ParallelOptions_WhenAutomatic_UsesMeasuredCrossoverAndWorkerCap()
    {
        WithGeneratedJson(10, path =>
        {
            var plan = Plan(path, null, [new SourceColumnRef("Index")]);
            var context = Context(plan, new Dictionary<string, string>());

            Assert.AreEqual(
                1,
                JsonParallelScanOptions.Resolve(
                    Snapshot(JsonParallelScanOptions.AutomaticCrossoverBytes - 1),
                    context));
            Assert.AreEqual(
                Math.Min(JsonParallelScanOptions.AutomaticMaximumParallelism, Environment.ProcessorCount),
                JsonParallelScanOptions.Resolve(
                    Snapshot(JsonParallelScanOptions.AutomaticCrossoverBytes),
                    context));
        });
    }

    [TestMethod]
    public void Schema_DeclaresOptionalMaximumParallelismSetting()
    {
        var requirement = new JsonSchema()
            .DescribeSourceRuntimeSettings("file", null!)
            .Single();

        Assert.AreEqual(JsonParallelScanOptions.MaximumParallelismSettingName, requirement.Name);
        Assert.IsFalse(requirement.Required);
        Assert.IsFalse(requirement.Secret);
        Assert.AreEqual(SourceRuntimeSettingPhase.Execution, requirement.Phases);
    }

    private static object[][] ReadRows(
        string path,
        SourceExecutionPlan plan,
        IReadOnlyDictionary<string, string> settings)
    {
        var context = Context(plan, settings);
        return new JsonSource(path, context).Chunks.SelectMany(chunk => chunk).ToArray();
    }

    private static SourceExecutionContext Context(
        SourceExecutionPlan plan,
        IReadOnlyDictionary<string, string> settings,
        DataSourceEventHandler? progress = null)
    {
        return RuntimeV2TestContexts.CreateExecutionContext(
            CancellationToken.None,
            [
                new SchemaColumn("Index", 0, typeof(long)),
                new SchemaColumn("Text", 1, typeof(string))
            ],
            settings,
            dataSourceProgressCallback: progress,
            executionPlan: plan);
    }

    private static SourceExecutionPlan Plan(
        string path,
        SourcePredicateExpression? predicate,
        IReadOnlyList<SourceColumnRef> requiredColumns,
        long? take = null)
    {
        var request = new SourcePlanRequest
        {
            Identity = SourceIdentity.Empty,
            RequiredColumns = requiredColumns,
            SourceRuntimeSettings = new Dictionary<string, string>(),
            Predicate = predicate,
            OrderBy = [],
            Skip = null,
            Take = take
        };
        return new JsonSchema().TryPlanSource("file", request, path).ExecutionPlan;
    }

    private static SourcePredicateComparison GreaterThanOrEqual(string name, long value)
    {
        return new SourcePredicateComparison(
            SourcePredicateComparisonOperator.GreaterOrEqual,
            new SourcePredicateColumn(new SourceColumnRef(name)),
            new SourcePredicateLiteral(value));
    }

    private static Dictionary<string, string> MaximumParallelism(string value)
    {
        return new Dictionary<string, string>
        {
            [JsonParallelScanOptions.MaximumParallelismSettingName] = value
        };
    }

    private static void WithGeneratedJson(int rowCount, Action<string> assertion)
    {
        var path = TempPath();
        try
        {
            using (var stream = File.Create(path))
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartArray();
                for (var row = 0; row < rowCount; row++)
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("Index", row);
                    writer.WriteNumber("Group", row % 10);
                    writer.WriteString("Text", $"row-{row}");
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            assertion(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string TempPath()
    {
        return Path.Combine(Path.GetTempPath(), $"musoq-json-parallel-{Guid.NewGuid():N}.json");
    }

    private static string FlattenMessages(Exception exception)
    {
        var messages = string.Empty;
        for (var current = exception; current is not null; current = current.InnerException)
            messages += current.Message + Environment.NewLine;
        return messages;
    }

    private static StructuredSchemaSnapshot Snapshot(long length)
    {
        return new StructuredSchemaSnapshot(
            new StructuredFileIdentity("synthetic.json", length, 0, "json", default),
            [],
            4,
            [
                new StructuredPartition(0, length / 4, 0, 1),
                new StructuredPartition(length / 4, length / 2, 1, 1),
                new StructuredPartition(length / 2, length * 3 / 4, 2, 1),
                new StructuredPartition(length * 3 / 4, length, 3, 1)
            ]);
    }
}
