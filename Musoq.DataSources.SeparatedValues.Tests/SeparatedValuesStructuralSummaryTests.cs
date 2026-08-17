#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Structured;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues.Tests;

[TestClass]
[DoNotParallelize]
public class SeparatedValuesStructuralSummaryTests
{
    [TestMethod]
    public void CompletedSequentialScan_ReplaysExactCountWithoutCreatingSidecars()
    {
        WithCsv(CreateRows(32), (path, directory) =>
        {
            var firstPlan = Plan(path, [new SourceColumnRef("Value")]);
            Assert.IsNull(SeparatedValuesSourceContract.From(firstPlan).StructuralSummary);

            var firstRows = ReadRows(path, firstPlan);
            Assert.AreEqual(32, firstRows.Length);

            var replayPlan = Plan(path, []);
            var replayContract = SeparatedValuesSourceContract.From(replayPlan);
            Assert.IsTrue(replayContract.HasExactCardinality);
            Assert.IsNotNull(replayContract.StructuralSummary);
            Assert.AreEqual(32L, replayContract.Snapshot.RowCount);
            Assert.IsTrue(replayContract.Snapshot.Partitions.IsEmpty);

            var replayRows = ReadRows(path, replayPlan, []);
            Assert.AreEqual(32, replayRows.Length);
            Assert.IsTrue(replayRows.All(row => row.Length == 0));
            CollectionAssert.AreEqual(
                new[] { path },
                Directory.GetFiles(directory),
                "Structural summaries must remain memory-only.");
        });
    }

    [TestMethod]
    public void CompletedSequentialScan_UsesCoarseBlockForLaterSlice()
    {
        const int rowCount = 12_000;
        const int skip = 10_000;
        var padding = new string('x', 512);
        var contents = new StringBuilder("Value,Padding\n", rowCount * 520);
        for (var row = 0; row < rowCount; row++)
            contents.Append(row).Append(',').Append(padding).Append('\n');

        WithCsv(contents.ToString(), (path, _) =>
        {
            var completePlan = Plan(path, [new SourceColumnRef("Value")]);
            Assert.AreEqual(rowCount, ReadRows(path, completePlan).Length);

            var slicePlan = Plan(path, [new SourceColumnRef("Value")], skip, 2);
            var contract = SeparatedValuesSourceContract.From(slicePlan);
            Assert.IsNotNull(contract.StructuralSummary);
            Assert.IsTrue(contract.StructuralSummary.Blocks.Length > 1);
            Assert.IsTrue(contract.StructuralSummary.TryFindRow(skip, out var block));
            Assert.IsTrue(block.FirstRecordOffset > contract.DataStartOffset);
            Assert.IsTrue(block.StartRow <= skip);

            var rows = ReadRows(path, slicePlan);
            Assert.AreEqual(2, rows.Length);
            Assert.AreEqual((long?)skip, rows[0][0]);
            Assert.AreEqual((long?)(skip + 1), rows[1][0]);
        });
    }

    [TestMethod]
    public void EarlyTake_DoesNotPublishPartialSummary()
    {
        WithCsv(CreateRows(16), (path, _) =>
        {
            var takePlan = Plan(path, [new SourceColumnRef("Value")], take: 1);
            Assert.AreEqual(1, ReadRows(path, takePlan).Length);

            var nextPlan = Plan(path, [new SourceColumnRef("Value")]);
            var nextContract = SeparatedValuesSourceContract.From(nextPlan);
            Assert.IsNull(nextContract.StructuralSummary);
            Assert.IsFalse(nextContract.HasExactCardinality);
        });
    }

    [TestMethod]
    public void CompletedParallelScan_PublishesOrderedSummary()
    {
        WithCsv(CreateRows(128), (path, _) =>
        {
            var plan = Plan(path, [new SourceColumnRef("Value")]);
            var pipeline = new SeparatedValuesScanPipeline(
                new SeparatedValuesParallelBlockScanPipeline(blockSize: 17),
                forceParallel: true);

            Assert.AreEqual(128, ReadRows(path, plan, pipeline: pipeline).Length);

            var replayContract = SeparatedValuesSourceContract.From(
                Plan(path, [new SourceColumnRef("Value")]));
            Assert.IsTrue(replayContract.HasExactCardinality);
            Assert.IsNotNull(replayContract.StructuralSummary);
            Assert.AreEqual(128L, replayContract.StructuralSummary.TotalRows);
            Assert.IsTrue(replayContract.StructuralSummary.Blocks.Length > 0);
        });
    }

    [TestMethod]
    public void ChangedFileIdentity_DoesNotReuseCompletedSummary()
    {
        WithCsv(CreateRows(8), (path, _) =>
        {
            var plan = Plan(path, [new SourceColumnRef("Value")]);
            Assert.AreEqual(8, ReadRows(path, plan).Length);
            Assert.IsNotNull(SeparatedValuesSourceContract.From(
                Plan(path, [new SourceColumnRef("Value")])).StructuralSummary);

            File.AppendAllText(path, "8,payload\n", new UTF8Encoding(false, true));

            var changedContract = SeparatedValuesSourceContract.From(
                Plan(path, [new SourceColumnRef("Value")]));
            Assert.IsNull(changedContract.StructuralSummary);
            Assert.IsFalse(changedContract.HasExactCardinality);
        });
    }

    [TestMethod]
    public void CachedSummary_DoesNotMoveSkipAheadOfPredicate()
    {
        WithCsv("Value,Padding\n1,a\n2,b\n3,c\n4,d\n", (path, _) =>
        {
            var completePlan = Plan(path, [new SourceColumnRef("Value")]);
            Assert.AreEqual(4, ReadRows(path, completePlan).Length);

            var predicate = new SourcePredicateComparison(
                SourcePredicateComparisonOperator.GreaterThan,
                new SourcePredicateColumn(new SourceColumnRef("Value")),
                new SourcePredicateLiteral(1L));
            var slicePlan = Plan(path, [new SourceColumnRef("Value")], 1, 1, predicate);
            Assert.IsNotNull(SeparatedValuesSourceContract.From(slicePlan).StructuralSummary);

            var rows = ReadRows(path, slicePlan);
            Assert.AreEqual(1, rows.Length);
            Assert.AreEqual((long?)3, rows[0][0]);
        });
    }

    [TestMethod]
    public void MemoryCache_EvictsLeastRecentlyUsedIdentityAtEntryLimit()
    {
        SeparatedValuesStructuralSummaryCache.Clear();
        try
        {
            var identities = Enumerable.Range(0, SeparatedValuesStructuralSummaryCache.MaximumEntries + 1)
                .Select(index => new StructuredFileIdentity(
                    $"summary-{index}.csv",
                    0,
                    index,
                    "separator=,",
                    new StructuredFileFingerprint(0, (ulong)index)))
                .ToArray();
            foreach (var identity in identities)
            {
                SeparatedValuesStructuralSummaryCache.Store(
                    new SeparatedValuesStructuralSummary(identity, 0, 0, []));
            }

            Assert.IsFalse(SeparatedValuesStructuralSummaryCache.TryGet(identities[0], out _));
            Assert.IsTrue(SeparatedValuesStructuralSummaryCache.TryGet(identities[^1], out _));
        }
        finally
        {
            SeparatedValuesStructuralSummaryCache.Clear();
        }
    }

    [TestMethod]
    public void SummaryBuilder_CoarsensRangesToBoundMetadataIndependentlyOfFileSize()
    {
        const int physicalRangeSize = 2 * 1024 * 1024;
        var rangeCount = SeparatedValuesStructuralSummaryBuilder.MaximumBlocksPerSummary * 2;
        var identity = new StructuredFileIdentity(
            "large-summary.csv",
            rangeCount * (long)physicalRangeSize,
            0,
            "separator=,",
            default);
        var builder = new SeparatedValuesStructuralSummaryBuilder(identity, 0);
        for (var index = 0; index < rangeCount; index++)
        {
            builder.ObserveRange(
                index,
                1,
                index * (long)physicalRangeSize,
                (index + 1L) * physicalRangeSize);
        }

        var summary = builder.Build();
        Assert.AreEqual(rangeCount, summary.TotalRows);
        Assert.IsTrue(summary.Blocks.Length <= SeparatedValuesStructuralSummaryBuilder.MaximumBlocksPerSummary);
    }

    private static SourceExecutionPlan Plan(
        string path,
        IReadOnlyList<SourceColumnRef> requiredColumns,
        long? skip = null,
        long? take = null,
        SourcePredicateExpression? predicate = null)
    {
        var request = new SourcePlanRequest
        {
            Identity = SourceIdentity.Empty,
            RequiredColumns = requiredColumns,
            SourceRuntimeSettings = Settings(),
            Predicate = predicate,
            OrderBy = [],
            Skip = skip,
            Take = take
        };
        return new SeparatedValuesSchema()
            .TryPlanSource("comma", request, path, true, 0)
            .ExecutionPlan;
    }

    private static object?[][] ReadRows(
        string path,
        SourceExecutionPlan plan,
        IReadOnlyCollection<ISchemaColumn>? columns = null,
        ISeparatedValuesScanPipeline? pipeline = null)
    {
        columns ??= [new SchemaColumn("Value", 0, typeof(long?))];
        var context = RuntimeV2TestContexts.CreateExecutionContext(
            allColumns: columns,
            sourceRuntimeSettings: new Dictionary<string, string>
            {
                [SeparatedValuesParallelScanOptions.MaximumParallelismSettingName] = "1"
            },
            executionPlan: plan);
        var source = pipeline is null
            ? new SeparatedValuesFromFileRowsSource(path, ",", true, 0, context)
            : new SeparatedValuesFromFileRowsSource(path, ",", true, 0, context, pipeline);
        return source.Chunks.SelectMany(chunk => chunk).ToArray();
    }

    private static Dictionary<string, string> Settings()
    {
        return new Dictionary<string, string>
        {
            [SeparatedValuesInferenceOptions.MaximumRowsSettingName] = "1",
            [SeparatedValuesInferenceOptions.MaximumTimeMillisecondsSettingName] = "1000"
        };
    }

    private static string CreateRows(int count)
    {
        var builder = new StringBuilder("Value,Padding\n");
        for (var row = 0; row < count; row++)
            builder.Append(row).Append(",payload\n");
        return builder.ToString();
    }

    private static void WithCsv(string contents, Action<string, string> assertion)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"musoq-csv-summary-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "data.csv");
        File.WriteAllText(path, contents, new UTF8Encoding(false, true));
        try
        {
            assertion(path, directory);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
