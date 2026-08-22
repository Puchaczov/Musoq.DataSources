#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Common;
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
                Assert.AreEqual(sequential[index], parallel[index]);
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

            var rows = SeparatedValuesNativeTestSource.Create<long>(path, ",", true, 0, context)
                .Chunks
                .SelectMany(chunk => chunk)
                .ToArray();

            Assert.AreEqual(4_000, rows.Length);
            Assert.AreEqual(0, capture.For("separated_values", DataSourcePhase.RowsKnown).Count);
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
            var budget = new SeparatedValuesOutputMemoryBudget(128 * 1024, 1024);
            var pipeline = new SeparatedValuesScanPipeline(
                new SeparatedValuesParallelBlockScanPipeline(
                    blockSize: 4096,
                    outputMemoryBudget: budget),
                forceParallel: true);
            var source = SeparatedValuesNativeTestSource.Create<long>(
                path, ",", true, 0, context, pipeline);

            var exception = Assert.ThrowsExactly<FormatException>(() =>
                source.Chunks.SelectMany(chunk => chunk).ToArray());

            StringAssert.Contains(FlattenMessages(exception), "column 'Age'");
            Assert.AreEqual(0L, budget.CurrentReservedBytes);
            Assert.IsTrue(budget.HighWaterBytes <= budget.CapacityBytes);
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
            var budget = new SeparatedValuesOutputMemoryBudget(128 * 1024, 1024);
            var pipeline = new SeparatedValuesScanPipeline(
                new SeparatedValuesParallelBlockScanPipeline(
                    blockSize: 4096,
                    outputMemoryBudget: budget),
                forceParallel: true);
            var source = SeparatedValuesNativeTestSource.Create<long>(
                path, ",", true, 0, context, pipeline);

            Assert.ThrowsExactly<OperationCanceledException>(() =>
                source.Chunks.SelectMany(chunk => chunk).ToArray());
            Assert.AreEqual(0L, budget.CurrentReservedBytes);
            Assert.IsTrue(budget.HighWaterBytes <= budget.CapacityBytes);
        });
    }

    [TestMethod]
    public void FileRowsSource_ConcurrentParallelQueries_ShareThePipelineWithoutCorruptingOrder()
    {
        WithGeneratedCsv(25_000, path =>
        {
            var plan = Plan(
                path,
                GreaterThanOrEqual("Group", 7L),
                [new SourceColumnRef("Index"), new SourceColumnRef("Text")]);
            var budget = new SeparatedValuesOutputMemoryBudget(256 * 1024, 1024);
            var pipeline = new SeparatedValuesScanPipeline(
                new SeparatedValuesParallelBlockScanPipeline(
                    blockSize: 4096,
                    outputMemoryBudget: budget),
                forceParallel: true);
            var first = Task.Run(() => ReadRows(path, plan, MaximumParallelism("8"), pipeline));
            var second = Task.Run(() => ReadRows(path, plan, MaximumParallelism("8"), pipeline));

            Task.WaitAll(first, second);
            Assert.AreEqual(7_500, first.Result.Length);
            Assert.AreEqual(7_500, second.Result.Length);
            for (var index = 0; index < first.Result.Length; index++)
                Assert.AreEqual(first.Result[index], second.Result[index]);
            Assert.AreEqual(0L, budget.CurrentReservedBytes);
            Assert.IsTrue(budget.HighWaterBytes <= budget.CapacityBytes);
        });
    }

    [TestMethod]
    public void ParallelOptions_WhenSliceIsAccepted_ForceSequentialExecution()
    {
        WithGeneratedCsv(1_000, path =>
        {
            var plan = Plan(path, null, [new SourceColumnRef("Index")], take: 10);
            var snapshot = SeparatedValuesSourceContract.From(plan).Snapshot;
            var context = Context(plan, MaximumParallelism("8"));

            Assert.AreEqual(1, SeparatedValuesParallelScanOptions.Resolve(snapshot, context));
        });
    }

    [TestMethod]
    public void ParallelOptions_CountableSlicesAreSupportedButPredicateSlicesAreNot()
    {
        WithGeneratedCsv(10_000, path =>
        {
            var positiveSkip = Plan(
                path,
                null,
                [new SourceColumnRef("Index")],
                take: 10,
                skip: 1);
            var largeTake = Plan(
                path,
                null,
                [new SourceColumnRef("Index")],
                take: SeparatedValuesParallelScanOptions.SequentialTakeThreshold + 1);
            var predicateSlice = Plan(
                path,
                GreaterThanOrEqual("Group", 5),
                [new SourceColumnRef("Index")],
                take: 5000,
                skip: 1);

            Assert.IsTrue(SeparatedValuesParallelScanOptions.IsParallelShapeSupported(
                Context(positiveSkip, MaximumParallelism("4"))));
            Assert.IsTrue(SeparatedValuesParallelScanOptions.IsParallelShapeSupported(
                Context(largeTake, MaximumParallelism("4"))));
            Assert.IsFalse(SeparatedValuesParallelScanOptions.IsParallelShapeSupported(
                Context(predicateSlice, MaximumParallelism("4"))));
        });
    }

    [TestMethod]
    public void ParallelDispatch_UsesOnlyCountableSliceShapes()
    {
        WithGeneratedCsv(10_000, path =>
        {
            var countablePlan = Plan(
                path,
                null,
                [new SourceColumnRef("Index")],
                take: 10,
                skip: 1);
            var countableCapture = new CapturingParallelPipeline();
            _ = ReadRows(
                path,
                countablePlan,
                MaximumParallelism("4"),
                new SeparatedValuesScanPipeline(countableCapture, forceParallel: true));
            Assert.AreEqual(1, countableCapture.Calls);

            var smallTakePlan = Plan(path, null, [new SourceColumnRef("Index")], take: 10);
            var smallTakeCapture = new CapturingParallelPipeline();
            var smallTakeRows = ReadRows(
                path,
                smallTakePlan,
                MaximumParallelism("4"),
                new SeparatedValuesScanPipeline(smallTakeCapture, forceParallel: true));
            Assert.AreEqual(10, smallTakeRows.Length);
            Assert.AreEqual(0, smallTakeCapture.Calls);

            var predicatePlan = Plan(
                path,
                GreaterThanOrEqual("Group", 5),
                [new SourceColumnRef("Index")],
                take: 5000,
                skip: 1);
            var predicateCapture = new CapturingParallelPipeline();
            var predicateRows = ReadRows(
                path,
                predicatePlan,
                MaximumParallelism("4"),
                new SeparatedValuesScanPipeline(predicateCapture, forceParallel: true));
            Assert.AreEqual(4999, predicateRows.Length);
            Assert.AreEqual(0, predicateCapture.Calls);
        });
    }

    [TestMethod]
    public void ParallelSlice_ColdAndWarmScansPreserveOrderingAtEveryWorkerSetting()
    {
        WithGeneratedCsv(20_000, path =>
        {
            var coldPlan = Plan(
                path,
                null,
                [new SourceColumnRef("Index"), new SourceColumnRef("Text")],
                take: 7000,
                skip: 5000);
            var expected = ReadRows(path, coldPlan, MaximumParallelism("1"));

            foreach (var workers in new[] { "2", "4", "8" })
            {
                var pipeline = new SeparatedValuesScanPipeline(
                    new SeparatedValuesParallelBlockScanPipeline(blockSize: 257),
                    forceParallel: true);
                var actual = ReadRows(path, coldPlan, MaximumParallelism(workers), pipeline);
                AssertRowsEqual(expected, actual, $"cold workers={workers}");
            }

            var completePlan = Plan(
                path,
                null,
                [new SourceColumnRef("Index")]);
            Assert.AreEqual(20_000, ReadRows(path, completePlan, MaximumParallelism("1")).Length);
            var warmPlan = Plan(
                path,
                null,
                [new SourceColumnRef("Index"), new SourceColumnRef("Text")],
                take: 7000,
                skip: 5000);
            Assert.IsNotNull(SeparatedValuesSourceContract.From(warmPlan).StructuralSummary);

            foreach (var workers in new[] { "2", "4", "8" })
            {
                var pipeline = new SeparatedValuesScanPipeline(
                    new SeparatedValuesParallelBlockScanPipeline(blockSize: 257),
                    forceParallel: true);
                var actual = ReadRows(path, warmPlan, MaximumParallelism(workers), pipeline);
                AssertRowsEqual(expected, actual, $"warm workers={workers}");
            }
        });
    }

    [TestMethod]
    public void ParallelSlice_LargeStandaloneTakeProcessesOnlyItsPrefix()
    {
        WithGeneratedCsv(10_000, path =>
        {
            var plan = Plan(
                path,
                null,
                [new SourceColumnRef("Index")],
                take: SeparatedValuesParallelScanOptions.SequentialTakeThreshold + 1);
            var pipeline = new SeparatedValuesScanPipeline(
                new SeparatedValuesParallelBlockScanPipeline(blockSize: 257),
                forceParallel: true);

            var rows = ReadRows(path, plan, MaximumParallelism("4"), pipeline);

            Assert.AreEqual(4097, rows.Length);
            Assert.AreEqual(0L, rows[0][0]);
            Assert.AreEqual(4096L, rows[^1][0]);
            Assert.IsNull(SeparatedValuesSourceContract.From(
                Plan(path, null, [new SourceColumnRef("Index")])).StructuralSummary);
        });
    }

    [TestMethod]
    public void ParallelSlice_ValidatesOnlyTheSelectedValueWindow()
    {
        var builder = new StringBuilder("Index,Group,Text\n");
        for (var row = 0; row < 10_000; row++)
        {
            if (row == 4500)
                builder.Append("not-a-number,0,before,extra\n");
            else if (row == 9500)
                builder.Append("\"unterminated");
            else
                builder.Append(row).Append(',').Append(row % 10).Append(",row-").Append(row).Append('\n');
        }

        WithCsv(builder.ToString(), path =>
        {
            var plan = Plan(
                path,
                null,
                [new SourceColumnRef("Index")],
                take: 4097,
                skip: 5000);
            var pipeline = new SeparatedValuesScanPipeline(
                new SeparatedValuesParallelBlockScanPipeline(blockSize: 257),
                forceParallel: true);

            var sequential = ReadRows(path, plan, MaximumParallelism("1"));
            var rows = ReadRows(path, plan, MaximumParallelism("4"), pipeline);

            Assert.AreEqual(4097, rows.Length);
            Assert.AreEqual(5000L, rows[0][0]);
            Assert.AreEqual(9096L, rows[^1][0]);
            AssertRowsEqual(sequential, rows, "selected validation window");
        });
    }

    [TestMethod]
    public void ParallelSlice_InvalidValueInsideSelectedWindowStillFails()
    {
        var builder = new StringBuilder("Index,Group,Text\n");
        for (var row = 0; row < 10_000; row++)
        {
            if (row == 7000)
                builder.Append("not-a-number,0,selected\n");
            else
                builder.Append(row).Append(',').Append(row % 10).Append(",row-").Append(row).Append('\n');
        }

        WithCsv(builder.ToString(), path =>
        {
            var plan = Plan(
                path,
                null,
                [new SourceColumnRef("Index")],
                take: 4097,
                skip: 5000);
            var pipeline = new SeparatedValuesScanPipeline(
                new SeparatedValuesParallelBlockScanPipeline(blockSize: 257),
                forceParallel: true);

            var exception = Assert.ThrowsExactly<FormatException>(() =>
                ReadRows(path, plan, MaximumParallelism("4"), pipeline));

            StringAssert.Contains(exception.Message, "row 7");
            StringAssert.Contains(exception.Message, "column 'Index'");
        });
    }

    [TestMethod]
    public void ParallelSlice_QuotedMultilineRecordsMatchSequential()
    {
        var builder = new StringBuilder("Index,Group,Text\n");
        for (var row = 0; row < 6000; row++)
        {
            builder.Append(row).Append(',').Append(row % 10).Append(',');
            if (row % 100 == 0)
                builder.Append('"').Append("line-").Append(row).Append("\ncontinued,\"\"quoted\"\"").Append('"');
            else
                builder.Append("row-").Append(row);
            builder.Append('\n');
        }

        WithCsv(builder.ToString(), path =>
        {
            var plan = Plan(
                path,
                null,
                [new SourceColumnRef("Index"), new SourceColumnRef("Text")],
                take: 4500,
                skip: 1000);
            var expected = ReadRows(path, plan, MaximumParallelism("1"));
            var pipeline = new SeparatedValuesScanPipeline(
                new SeparatedValuesParallelBlockScanPipeline(blockSize: 31),
                forceParallel: true);

            var actual = ReadRows(path, plan, MaximumParallelism("4"), pipeline);

            AssertRowsEqual(expected, actual, "quoted multiline slice");
        });
    }

    [TestMethod]
    public void ParallelOptions_WhenSettingIsInvalid_ThrowsClearError()
    {
        WithGeneratedCsv(100, path =>
        {
            var plan = Plan(path, null, [new SourceColumnRef("Index")]);
            var snapshot = SeparatedValuesSourceContract.From(plan).Snapshot;
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
                SeparatedValuesParallelScanOptions.AutomaticMaximumParallelism,
                SeparatedValuesParallelScanOptions.Resolve(
                    Snapshot(SeparatedValuesParallelScanOptions.AutomaticCrossoverBytes),
                    context));
        });
    }

    [TestMethod]
    public void ParallelOptions_WhenPredicateIsAccepted_UsesTheLargeFileCrossover()
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
                    Snapshot(SeparatedValuesParallelScanOptions.AutomaticCrossoverBytes - 1),
                    context));
            Assert.AreEqual(
                SeparatedValuesParallelScanOptions.AutomaticMaximumParallelism,
                SeparatedValuesParallelScanOptions.Resolve(
                    Snapshot(SeparatedValuesParallelScanOptions.AutomaticCrossoverBytes),
                    context));
        });
    }

    [TestMethod]
    public void Schema_DeclaresOptionalMaximumParallelismSetting()
    {
        var requirements = new SeparatedValuesSchema()
            .DescribeSourceRuntimeSettings("comma", null!)
            .ToDictionary(requirement => requirement.Name);
        var requirement = requirements[SeparatedValuesParallelScanOptions.MaximumParallelismSettingName];

        Assert.AreEqual(SeparatedValuesParallelScanOptions.MaximumParallelismSettingName, requirement.Name);
        Assert.IsFalse(requirement.Required);
        Assert.IsFalse(requirement.Secret);
        Assert.AreEqual(SourceRuntimeSettingPhase.Execution, requirement.Phases);
        Assert.AreEqual(4, requirements.Count);
        Assert.AreEqual(
            SourceRuntimeSettingPhase.Metadata | SourceRuntimeSettingPhase.Planning,
            requirements[SeparatedValuesInferenceOptions.MaximumBytesSettingName].Phases);
        Assert.AreEqual(
            SourceRuntimeSettingPhase.Metadata | SourceRuntimeSettingPhase.Planning,
            requirements[SeparatedValuesInferenceOptions.MaximumRowsSettingName].Phases);
        Assert.AreEqual(
            SourceRuntimeSettingPhase.Metadata | SourceRuntimeSettingPhase.Planning,
            requirements[SeparatedValuesInferenceOptions.MaximumTimeMillisecondsSettingName].Phases);
    }

    private static ParallelTestRow[] ReadRows(
        string path,
        SourceExecutionPlan plan,
        IReadOnlyDictionary<string, string> settings,
        ISeparatedValuesQueryScanPipeline? pipeline = null)
    {
        var context = Context(plan, settings);
        if (plan.AcceptedColumns.Count == 1)
        {
            return SeparatedValuesNativeTestSource.Create<long>(path, ",", true, 0, context, pipeline)
                .Chunks
                .SelectMany(chunk => chunk)
                .Select(static row => new ParallelTestRow(row.Item0, null))
                .ToArray();
        }

        return SeparatedValuesNativeTestSource.Create<long, string>(path, ",", true, 0, context, pipeline)
            .Chunks
            .SelectMany(chunk => chunk)
            .Select(static row => new ParallelTestRow(row.Item0, row.Item1))
            .ToArray();
    }

    private static SourceExecutionContext Context(
        SourceExecutionPlan plan,
        IReadOnlyDictionary<string, string> settings,
        DataSourceEventHandler? progress = null)
    {
        var columns = plan.AcceptedColumns
            .Select((column, index) => new SchemaColumn(
                column.Name,
                index,
                column.Name == "Index" ? typeof(long) : typeof(string)))
            .ToArray();
        return RuntimeV2TestContexts.CreateExecutionContext(
            CancellationToken.None,
            columns,
            settings,
            dataSourceProgressCallback: progress,
            executionPlan: plan);
    }

    private static SourceExecutionPlan Plan(
        string path,
        SourcePredicateExpression? predicate,
        IReadOnlyList<SourceColumnRef> requiredColumns,
        long? take = null,
        long? skip = null)
    {
        var request = new SourcePlanRequest
        {
            Identity = SourceIdentity.Empty,
            RequiredColumns = requiredColumns,
            SourceRuntimeSettings = new Dictionary<string, string>(),
            Predicate = predicate,
            OrderBy = [],
            Skip = skip,
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

    private static void AssertRowsEqual(ParallelTestRow[] expected, ParallelTestRow[] actual, string message)
    {
        Assert.AreEqual(expected.Length, actual.Length, message);
        for (var index = 0; index < expected.Length; index++)
            Assert.AreEqual(expected[index], actual[index], $"{message}, row={index}");
    }

    private readonly record struct ParallelTestRow(long Item0, string? Item1)
    {
        public object? this[int index] => index switch
        {
            0 => Item0,
            1 => Item1,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };
    }

    private sealed class CapturingParallelPipeline : ISeparatedValuesParallelQueryScanPipeline
    {
        public int Calls { get; private set; }

        public long Execute<TRow, TMaterializer>(
            SeparatedValuesScanRequest request,
            SeparatedValuesSourceContract contract,
            SeparatedValuesQueryShapeMapping mapping,
            QueryRowShape shape,
            IChunkWriter<TRow> writer,
            DataSourceProgressReporter progress,
            int chunkSize,
            int workerCount,
            CancellationToken cancellationToken)
            where TMaterializer : struct, IQueryRowMaterializer<TRow>
        {
            Calls++;
            return 0;
        }
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
