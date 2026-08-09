#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Structured;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues.Tests;

[TestClass]
public class SeparatedValuesParallelExecutionTests
{
    [TestMethod]
    public void FileRowsSource_WhenParallelismIsForced_MatchesSequentialRowsAndOrder()
    {
        WithGeneratedCsv(20_000, path =>
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
    public void FileRowsSource_WhenParallelismIsForced_ReportsIdenticalProgress()
    {
        WithGeneratedCsv(20_000, path =>
        {
            SourceColumnRef[] required = [new("Index")];
            var plan = Plan(path, GreaterThanOrEqual("Group", 8L), required);
            var capture = new DataSourceProgressCapture();
            var context = Context(plan, MaximumParallelism("4"), capture.Handler);

            var rows = new SeparatedValuesFromFileRowsSource(path, ",", true, 0, context)
                .Chunks
                .SelectMany(chunk => chunk)
                .ToArray();

            Assert.AreEqual(4_000, rows.Length);
            Assert.AreEqual(20_000L, capture.For("separated_values", DataSourcePhase.RowsKnown).Single().TotalRows);
            Assert.AreEqual(20_000L, capture.For("separated_values", DataSourcePhase.RowsRead).Last().RowsProcessed);
            Assert.AreEqual(4_000L, capture.For("separated_values", DataSourcePhase.End).Single().RowsProcessed);
        });
    }

    [TestMethod]
    public void FileRowsSource_WhenParallelWorkerConversionFails_PropagatesOriginalError()
    {
        var builder = new StringBuilder("Age\n");
        for (var row = 0; row < 20_000; row++)
            builder.Append(row).Append('\n');
        builder.Append("not-a-number\n");

        WithCsv(builder.ToString(), path =>
        {
            SourceColumnRef[] required = [new("Age")];
            var plan = Plan(path, null, required);
            var context = RuntimeV2TestContexts.CreateExecutionContext(
                CancellationToken.None,
                [new SchemaColumn("Age", 0, typeof(long))],
                MaximumParallelism("4"),
                executionPlan: plan);
            var source = new SeparatedValuesFromFileRowsSource(path, ",", true, 0, context);

            var exception = Assert.ThrowsExactly<FormatException>(() =>
                source.Chunks.SelectMany(chunk => chunk).ToArray());

            StringAssert.Contains(FlattenMessages(exception), "column 'Age'");
        });
    }

    [TestMethod]
    public void FileRowsSource_WhenParallelScanIsCancelled_StopsWorkersWithoutDeadlock()
    {
        WithGeneratedCsv(100_000, path =>
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
            var source = new SeparatedValuesFromFileRowsSource(path, ",", true, 0, context);

            Assert.ThrowsExactly<OperationCanceledException>(() =>
                source.Chunks.SelectMany(chunk => chunk).ToArray());
        });
    }

    [TestMethod]
    public void ParallelOptions_WhenSliceIsAccepted_ForceSequentialExecution()
    {
        WithGeneratedCsv(1_000, path =>
        {
            var plan = Plan(path, null, [new SourceColumnRef("Index")], take: 10);
            var snapshot = SeparatedValuesSchemaDiscovery.GetSnapshot(path, ",", true, 0);
            var context = Context(plan, MaximumParallelism("8"));

            Assert.AreEqual(1, SeparatedValuesParallelScanOptions.Resolve(snapshot, context));
        });
    }

    [TestMethod]
    public void ParallelOptions_WhenSettingIsInvalid_ThrowsClearError()
    {
        WithGeneratedCsv(100, path =>
        {
            var plan = Plan(path, null, [new SourceColumnRef("Index")]);
            var snapshot = SeparatedValuesSchemaDiscovery.GetSnapshot(path, ",", true, 0);
            var context = Context(plan, MaximumParallelism("many"));

            var exception = Assert.ThrowsExactly<ArgumentException>(() =>
                SeparatedValuesParallelScanOptions.Resolve(snapshot, context));

            StringAssert.Contains(exception.Message, SeparatedValuesParallelScanOptions.MaximumParallelismSettingName);
        });
    }

    [TestMethod]
    public void ParallelOptions_WhenAutomatic_UsesMeasuredCrossoverAndWorkerCap()
    {
        WithGeneratedCsv(10, path =>
        {
            var plan = Plan(path, null, [new SourceColumnRef("Index")]);
            var context = Context(plan, new Dictionary<string, string>());

            Assert.AreEqual(
                1,
                SeparatedValuesParallelScanOptions.Resolve(
                    Snapshot(SeparatedValuesParallelScanOptions.AutomaticCrossoverBytes - 1),
                    context));
            Assert.AreEqual(
                Math.Min(SeparatedValuesParallelScanOptions.AutomaticMaximumParallelism, Environment.ProcessorCount),
                SeparatedValuesParallelScanOptions.Resolve(
                    Snapshot(SeparatedValuesParallelScanOptions.AutomaticCrossoverBytes),
                    context));
        });
    }

    [TestMethod]
    public void ParallelOptions_WhenPredicateIsAccepted_UsesSelectiveQueryCrossover()
    {
        WithGeneratedCsv(10, path =>
        {
            var plan = Plan(
                path,
                GreaterThanOrEqual("Index", 5),
                [new SourceColumnRef("Index")]);
            var context = Context(plan, new Dictionary<string, string>());

            Assert.AreEqual(
                1,
                SeparatedValuesParallelScanOptions.Resolve(
                    Snapshot(SeparatedValuesParallelScanOptions.PredicateCrossoverBytes - 1),
                    context));
            Assert.AreEqual(
                Math.Min(SeparatedValuesParallelScanOptions.AutomaticMaximumParallelism, Environment.ProcessorCount),
                SeparatedValuesParallelScanOptions.Resolve(
                    Snapshot(SeparatedValuesParallelScanOptions.PredicateCrossoverBytes),
                    context));
        });
    }

    [TestMethod]
    public void Schema_DeclaresOptionalMaximumParallelismSetting()
    {
        var requirement = new SeparatedValuesSchema()
            .DescribeSourceRuntimeSettings("comma", null!)
            .Single();

        Assert.AreEqual(SeparatedValuesParallelScanOptions.MaximumParallelismSettingName, requirement.Name);
        Assert.IsFalse(requirement.Required);
        Assert.IsFalse(requirement.Secret);
        Assert.AreEqual(SourceRuntimeSettingPhase.Execution, requirement.Phases);
    }

    private static object?[][] ReadRows(
        string path,
        SourceExecutionPlan plan,
        IReadOnlyDictionary<string, string> settings)
    {
        var context = Context(plan, settings);
        return new SeparatedValuesFromFileRowsSource(path, ",", true, 0, context)
            .Chunks
            .SelectMany(chunk => chunk)
            .ToArray();
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
        return new SeparatedValuesSchema()
            .TryPlanSource("comma", request, path, true, 0)
            .ExecutionPlan;
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
            [SeparatedValuesParallelScanOptions.MaximumParallelismSettingName] = value
        };
    }

    private static void WithGeneratedCsv(int rowCount, Action<string> assertion)
    {
        var builder = new StringBuilder("Index,Group,Text\n");
        for (var row = 0; row < rowCount; row++)
        {
            builder.Append(row)
                .Append(',')
                .Append(row % 10)
                .Append(',')
                .Append("row-")
                .Append(row)
                .Append('\n');
        }

        WithCsv(builder.ToString(), assertion);
    }

    private static void WithCsv(string contents, Action<string> assertion)
    {
        var path = Path.Combine(Path.GetTempPath(), $"musoq-csv-parallel-{Guid.NewGuid():N}.csv");
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
            new StructuredFileIdentity("synthetic.csv", length, 0, "csv", default),
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
