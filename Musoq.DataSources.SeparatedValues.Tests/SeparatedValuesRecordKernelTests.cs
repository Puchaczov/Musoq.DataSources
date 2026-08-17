#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues.Tests;

[TestClass]
public class SeparatedValuesRecordKernelTests
{
    [TestMethod]
    public void AcceptedRecord_IsWalkedOnceAndMaterializedOnlyOnDemand()
    {
        WithCsv("A,B,C\n1,2,3\n", path =>
        {
            var (contract, context) = CreateKernelContext(path, 1L);
            var kernel = SeparatedValuesRecordKernel.Create(contract, context);
            using var reader = new SeparatedValuesUtf8Reader(path, (byte)',');
            Assert.IsTrue(reader.TryRead(out _));
            Assert.IsTrue(reader.TryRead(out var record));

            Assert.IsTrue(kernel.Prepare(record, 1));
            Assert.AreEqual(3L, kernel.FieldsVisited);
            Assert.AreEqual(0L, kernel.MaterializedRowCount);

            var row = kernel.Materialize(record, 1);

            Assert.AreEqual(3L, kernel.FieldsVisited);
            Assert.AreEqual(1L, kernel.MaterializedRowCount);
            Assert.AreEqual(3L, row[0]);
        });
    }

    [TestMethod]
    public void RejectedRecord_DoesNotAllocateAnEvaluatorRow()
    {
        WithCsv("A,B,C\n1,2,3\n", path =>
        {
            var (contract, context) = CreateKernelContext(path, 9L);
            var kernel = SeparatedValuesRecordKernel.Create(contract, context);
            using var reader = new SeparatedValuesUtf8Reader(path, (byte)',');
            Assert.IsTrue(reader.TryRead(out _));
            Assert.IsTrue(reader.TryRead(out var record));

            Assert.IsFalse(kernel.Prepare(record, 1));

            Assert.AreEqual(3L, kernel.FieldsVisited);
            Assert.AreEqual(0L, kernel.MaterializedRowCount);
        });
    }

    private static (SeparatedValuesSourceContract Contract, SourceExecutionContext Context) CreateKernelContext(
        string path,
        long expected)
    {
        var predicate = new SourcePredicateComparison(
            SourcePredicateComparisonOperator.Equal,
            new SourcePredicateColumn(new SourceColumnRef("A")),
            new SourcePredicateLiteral(expected));
        var settings = new Dictionary<string, string>
        {
            [SeparatedValuesInferenceOptions.MaximumTimeMillisecondsSettingName] = "1000"
        };
        var request = new SourcePlanRequest
        {
            Identity = SourceIdentity.Empty,
            RequiredColumns = [new SourceColumnRef("C")],
            SourceRuntimeSettings = settings,
            Predicate = predicate,
            OrderBy = [],
            Skip = null,
            Take = null
        };
        var plan = new SeparatedValuesSchema()
            .TryPlanSource("comma", request, path, true, 0)
            .ExecutionPlan;
        var context = RuntimeV2TestContexts.CreateExecutionContext(
            allColumns: [new SchemaColumn("C", 0, typeof(long?))],
            executionPlan: plan);
        return (SeparatedValuesSourceContract.From(plan), context);
    }

    private static void WithCsv(string contents, Action<string> assertion)
    {
        var path = Path.Combine(Path.GetTempPath(), $"musoq-csv-kernel-{Guid.NewGuid():N}.csv");
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
