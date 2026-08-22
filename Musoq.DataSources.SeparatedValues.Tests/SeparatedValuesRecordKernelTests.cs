#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Structured;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;
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
            var (kernel, projector) = CreateQueryKernel(contract, context);
            using var reader = new SeparatedValuesUtf8Reader(path, (byte)',');
            Assert.IsTrue(reader.TryRead(out _));
            Assert.IsTrue(reader.TryRead(out var record));

            Assert.IsTrue(kernel.Prepare(record, 1));
            Assert.AreEqual(3L, kernel.FieldsVisited);
            Assert.AreEqual(3L, kernel.ParsedFields);
            Assert.AreEqual(0L, kernel.MaterializedRowCount);

            var row = projector.Materialize(record, 1);

            Assert.AreEqual(3L, kernel.FieldsVisited);
            Assert.AreEqual(3L, kernel.ParsedFields, "Materialization must reuse sampled numeric scratch values.");
            Assert.AreEqual(1L, kernel.MaterializedRowCount);
            Assert.AreEqual(3L, row.Item0);
        });
    }

    [TestMethod]
    public void SampledFieldUsedByValidationPredicateAndProjection_IsParsedOnce()
    {
        WithCsv("A,B,C\n1,2,3\n", path =>
        {
            var predicate = new SourcePredicateComparison(
                SourcePredicateComparisonOperator.Equal,
                new SourcePredicateColumn(new SourceColumnRef("C")),
                new SourcePredicateLiteral(3L));
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
            var plan = new SeparatedValuesSchema().TryPlanSource("comma", request, path, true, 0).ExecutionPlan;
            var context = RuntimeV2TestContexts.CreateExecutionContext(
                allColumns: [new SchemaColumn("C", 0, typeof(long?))],
                executionPlan: plan);
            var (kernel, projector) = CreateQueryKernel(SeparatedValuesSourceContract.From(plan), context);
            using var reader = new SeparatedValuesUtf8Reader(path, (byte)',');
            Assert.IsTrue(reader.TryRead(out _));
            Assert.IsTrue(reader.TryRead(out var record));

            Assert.IsTrue(kernel.Prepare(record, 1));
            Assert.AreEqual(3L, kernel.ParsedFields);
            Assert.AreEqual(3L, projector.Materialize(record, 1).Item0);
            Assert.AreEqual(3L, kernel.ParsedFields);
        });
    }

    [TestMethod]
    public void RejectedRecord_DoesNotAllocateAnEvaluatorRow()
    {
        WithCsv("A,B,C\n1,2,3\n", path =>
        {
            var (contract, context) = CreateKernelContext(path, 9L);
            var (kernel, _) = CreateQueryKernel(contract, context);
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

    private static (
        SeparatedValuesRecordKernel Kernel,
        SeparatedValuesQueryRowProjector<
            TestRow1<long?>,
            SeparatedValuesNativeTestSource.TestRow1Materializer<long?>> Projector) CreateQueryKernel(
        SeparatedValuesSourceContract contract,
        SourceExecutionContext context)
    {
        var layout = (StructuredExecutionLayout)context.Plan.Properties![
            SeparatedValuesPlanning.LayoutPropertyName]!;
        var shape = SeparatedValuesNativeTestSource.CreateShape(context, typeof(long?));
        Assert.IsTrue(SeparatedValuesQueryShapeMapping.TryCreate(
            contract,
            layout,
            context.AllColumns,
            shape,
            out var mapping,
            out var reason), reason);
        var kernel = SeparatedValuesRecordProgram
            .CompileQuery(contract, context, mapping!)
            .CreateExecutor();
        return (
            kernel,
            kernel.CreateQueryProjector<
                TestRow1<long?>,
                SeparatedValuesNativeTestSource.TestRow1Materializer<long?>>());
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
