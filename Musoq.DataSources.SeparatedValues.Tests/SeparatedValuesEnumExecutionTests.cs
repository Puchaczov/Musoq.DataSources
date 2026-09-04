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
public sealed class SeparatedValuesEnumExecutionTests
{
    [TestMethod]
    public void EnumPredicateAndProjection_ParseTouchedFieldOnceAndSkipRejectedFields()
    {
        WithCsv("Status,Access\n10,3\n20,1\n", path =>
        {
            var statusDescriptor = Descriptor("StatusKind", EnumUnderlyingKind.Int32, false, 10, 20);
            var accessDescriptor = Descriptor("AccessKind", EnumUnderlyingKind.UInt32, true, 1, 3);
            var (contract, context, shape) = CreateExecution(
                path,
                statusDescriptor,
                accessDescriptor,
                new SourcePredicateComparison(
                    SourcePredicateComparisonOperator.Equal,
                    new SourcePredicateColumn(new SourceColumnRef("Status")),
                    new SourcePredicateEnumLiteral(
                        EnumScalarValue.FromInt32(10),
                        statusDescriptor.Fingerprint)));
            var layout = (StructuredExecutionLayout)context.Plan.Properties![SeparatedValuesPlanning.LayoutPropertyName]!;
            Assert.IsTrue(SeparatedValuesQueryShapeMapping.TryCreate(
                contract,
                layout,
                context.AllColumns,
                shape,
                out var mapping,
                out var reason), reason);

            var kernel = SeparatedValuesRecordProgram.CompileQuery(contract, context, mapping!).CreateExecutor();
            var projector = kernel.CreateQueryProjector<
                TestRow2<int?, uint?>,
                SeparatedValuesNativeTestSource.TestRow2Materializer<int?, uint?>>();
            using var reader = new SeparatedValuesUtf8Reader(path, (byte)',');
            Assert.IsTrue(reader.TryRead(out _));

            Assert.IsTrue(reader.TryRead(out var accepted));
            Assert.IsTrue(kernel.Prepare(accepted, 1));
            Assert.AreEqual(2L, kernel.ParsedFields);
            var materialized = projector.Materialize(accepted, 1);
            Assert.AreEqual(10, materialized.Item0);
            Assert.AreEqual((uint)3, materialized.Item1);
            Assert.AreEqual(1L, kernel.MaterializedRowCount);
            Assert.AreEqual(2L, kernel.ParsedFields, "Projection must reuse the parsed enum values.");

            Assert.IsTrue(reader.TryRead(out var rejected));
            Assert.IsFalse(kernel.Prepare(rejected, 2));
            Assert.AreEqual(3L, kernel.ParsedFields,
                "Once Status rejects the row, the projected Access enum must not be decoded.");
            Assert.AreEqual(1L, kernel.MaterializedRowCount);
            Assert.AreEqual(4L, kernel.FieldsVisited);
        });
    }

    [TestMethod]
    public void EnumRows_ForcedSequentialAndParallelPipelinesProduceIdenticalPrimitiveValues()
    {
        var builder = new StringBuilder("Status,Access\n");
        for (var index = 0; index < 2048; index++)
            builder.Append(index % 2 == 0 ? "10,3\n" : "20,1\n");

        WithCsv(builder.ToString(), path =>
        {
            var statusDescriptor = Descriptor("StatusKind", EnumUnderlyingKind.Int32, false, 10, 20);
            var accessDescriptor = Descriptor("AccessKind", EnumUnderlyingKind.UInt32, true, 1, 3);
            var sequential = ReadRows(path, statusDescriptor, accessDescriptor, "1", forceParallel: false);
            var parallel = ReadRows(path, statusDescriptor, accessDescriptor, "4", forceParallel: true);

            Assert.AreEqual(sequential.Length, parallel.Length);
            CollectionAssert.AreEqual(sequential, parallel);
            Assert.IsTrue(sequential.All(row =>
                (row.Item0 == 10 || row.Item0 == 20) &&
                (row.Item1 == 1u || row.Item1 == 3u)));
        });
    }

    private static TestRow2<int?, uint?>[] ReadRows(
        string path,
        EnumTypeDescriptor statusDescriptor,
        EnumTypeDescriptor accessDescriptor,
        string maximumParallelism,
        bool forceParallel)
    {
        var (contract, context, shape) = CreateExecution(
            path,
            statusDescriptor,
            accessDescriptor,
            predicate: null,
            maximumParallelism);
        var pipeline = forceParallel
            ? new SeparatedValuesScanPipeline(
                new SeparatedValuesParallelBlockScanPipeline(),
                forceParallel: true)
            : new SeparatedValuesScanPipeline();
        var source = new SeparatedValuesQueryRowSource<
            TestRow2<int?, uint?>,
            SeparatedValuesNativeTestSource.TestRow2Materializer<int?, uint?>>(
            path,
            ",",
            true,
            0,
            new QueryScopedRowSourceRequest(context, shape),
            pipeline);
        var chunks = source.Chunks;
        using var disposableChunks = (IDisposable)chunks;
        using var enumerator = chunks.GetEnumerator();
        var rows = new List<TestRow2<int?, uint?>>();
        while (enumerator.MoveNext())
            rows.AddRange(enumerator.Current);
        return rows.ToArray();
    }

    private static (
        SeparatedValuesSourceContract Contract,
        SourceExecutionContext Context,
        QueryRowShape Shape) CreateExecution(
        string path,
        EnumTypeDescriptor statusDescriptor,
        EnumTypeDescriptor accessDescriptor,
        SourcePredicateExpression? predicate,
        string maximumParallelism = "1")
    {
        var statusType = typeof(int?);
        var accessType = typeof(uint?);
        var dialect = SeparatedValuesDialect.Strict((byte)',');
        var identity = StructuredFileIdentity.Capture(
            path,
            SeparatedValuesFormat.CreateParserOptions(dialect, true, 0));
        var snapshot = new StructuredSchemaSnapshot(
            identity,
            [
                new StructuredColumnSnapshot(
                    "Status", 0, new StructuredTypeState(StructuredValueKind.Long, true), 0,
                    statusType, statusType, statusDescriptor),
                new StructuredColumnSnapshot(
                    "Access", 1, new StructuredTypeState(StructuredValueKind.Long, true), 1,
                    accessType, accessType, accessDescriptor)
            ],
            2048);
        var contract = new SeparatedValuesSourceContract(
            snapshot,
            SeparatedValuesSchemaResolutionMode.Declared,
            hasExactCardinality: true,
            inspectedRows: 0,
            inspectedBytes: 0,
            elapsed: TimeSpan.Zero,
            dataStartOffset: Encoding.UTF8.GetByteCount("Status,Access\n"),
            dialect: dialect);
        var request = new SourcePlanRequest
        {
            Identity = new SourceIdentity("separatedvalues", "comma", identity.CanonicalPath, "enum-execution"),
            RequiredColumns = [new SourceColumnRef("Status"), new SourceColumnRef("Access")],
            Predicate = predicate,
            OrderBy = [],
            Skip = null,
            Take = null
        };
        var plan = SeparatedValuesSourcePlanner.Plan(contract, request).ExecutionPlan;
        var columns = new ISchemaColumn[]
        {
            new SchemaColumn("Status", 0, statusType, statusType, statusDescriptor),
            new SchemaColumn("Access", 1, accessType, accessType, accessDescriptor)
        };
        var context = RuntimeV2TestContexts.CreateExecutionContext(
            allColumns: columns,
            sourceRuntimeSettings: new Dictionary<string, string>
            {
                [SeparatedValuesParallelScanOptions.MaximumParallelismSettingName] = maximumParallelism
            },
            executionPlan: plan);
        var shape = SeparatedValuesNativeTestSource.CreateShape(context, statusType, accessType);
        return (contract, context, shape);
    }

    private static EnumTypeDescriptor Descriptor(
        string name,
        EnumUnderlyingKind kind,
        bool flags,
        int first,
        int second)
    {
        return new EnumTypeDescriptor(
            name,
            EnumTypeOrigin.QueryLocal,
            kind,
            flags,
            kind == EnumUnderlyingKind.Int32
                ? [
                    new EnumMemberDescriptor("Queued", EnumScalarValue.FromInt32(first)),
                    new EnumMemberDescriptor("Running", EnumScalarValue.FromInt32(second))
                ]
                : [
                    new EnumMemberDescriptor("Read", EnumScalarValue.FromUInt32((uint)first)),
                    new EnumMemberDescriptor("ReadWrite", EnumScalarValue.FromUInt32((uint)second))
                ]);
    }

    private static void WithCsv(string contents, Action<string> assertion)
    {
        var path = Path.Combine(Path.GetTempPath(), $"musoq-enum-execution-{Guid.NewGuid():N}.csv");
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
