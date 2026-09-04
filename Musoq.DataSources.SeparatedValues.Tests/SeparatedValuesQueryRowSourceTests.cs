#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Diagnostics;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues.Tests;

[TestClass]
[DoNotParallelize]
public sealed class SeparatedValuesQueryRowSourceTests
{
    [TestMethod]
    public void Schema_Constructors_HaveNoBooleanTransferToggle()
    {
        var booleanConstructors = typeof(SeparatedValuesSchema)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(static constructor => constructor.GetParameters()
                .Any(static parameter => parameter.ParameterType == typeof(bool)))
            .ToArray();

        Assert.AreEqual(0, booleanConstructors.Length);
    }

    [TestMethod]
    public void DescribeSource_WhenMetadataIsExact_RequiresQueryRowsAndKeepsNominalDeclaredRowType()
    {
        WithCsv("Name,Age\nAda,36\n", path =>
        {
            var metadata = MetadataContext([]);

            var descriptor = new SeparatedValuesSchema().DescribeSource(
                "comma",
                new SourceDescribeContext(CreateIdentity(), metadata),
                path,
                true,
                0);

            Assert.AreEqual(
                SourceTransferCapabilities.QueryScopedRows | SourceTransferCapabilities.LogicalScalarReads,
                descriptor.TransferCapabilities);
            Assert.AreEqual(typeof(object[]), descriptor.RowType);
        });
    }

    [TestMethod]
    public void DescribeSource_WhenMetadataHasUnsupportedModifier_ThrowsPreciseEligibilityError()
    {
        WithCsv("Value\nhttps://example.test\n", path =>
        {
            var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
                new SeparatedValuesSchema().DescribeSource(
                "comma",
                new SourceDescribeContext(
                    CreateIdentity(),
                    MetadataContext(
                    [
                        new SchemaColumn(
                            "Value",
                            0,
                            typeof(string),
                            new Dictionary<string, string> { ["unsupported"] = "value" })
                    ])),
                path,
                true,
                0));

            StringAssert.Contains(exception.Message, "separatedvalues.comma");
            StringAssert.Contains(exception.Message, "source:query-row-test");
            StringAssert.Contains(exception.Message, "unsupported read modifiers");
        });
    }

    [TestMethod]
    public void DescribeSource_WhenMetadataHasUnsupportedExactType_ThrowsPreciseEligibilityError()
    {
        WithCsv("Value\nhttps://example.test\n", path =>
        {
            var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
                new SeparatedValuesSchema().DescribeSource(
                    "comma",
                    new SourceDescribeContext(
                        CreateIdentity(),
                        MetadataContext([new SchemaColumn("Value", 0, typeof(Uri))])),
                    path,
                    true,
                    0));

            StringAssert.Contains(exception.Message, "column 'Value'");
            StringAssert.Contains(exception.Message, "unsupported exact type");
            StringAssert.Contains(exception.Message, typeof(Uri).ToString());
        });
    }

    [TestMethod]
    public void DescribeSource_WhenMetadataHasUnresolvedIntendedType_ThrowsPreciseEligibilityError()
    {
        WithCsv("Value\ntext\n", path =>
        {
            var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
                new SeparatedValuesSchema().DescribeSource(
                    "comma",
                    new SourceDescribeContext(
                        CreateIdentity(),
                        MetadataContext(
                            [new SchemaColumn("Value", 0, typeof(string), "Generated.PendingType")])),
                    path,
                    true,
                    0));

            StringAssert.Contains(exception.Message, "column 'Value'");
            StringAssert.Contains(exception.Message, "unresolved intended type 'Generated.PendingType'");
        });
    }

    [TestMethod]
    public void DescribeSource_WhenHeaderNamesAreCaseInsensitivelyDuplicated_ThrowsPreciseEligibilityError()
    {
        WithCsv("Name,name\nAda,Lovelace\n", path =>
        {
            var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
                new SeparatedValuesSchema().DescribeSource(
                    "comma",
                    new SourceDescribeContext(CreateIdentity(), MetadataContext([])),
                    path,
                    true,
                    0));

            StringAssert.Contains(exception.Message, "duplicate column name 'name'");
        });
    }

    [TestMethod]
    public void QuerySource_WhenProjectionIsReordered_ReadsPhysicalFieldsIntoStructCarrier()
    {
        WithCsv("Name,Age,Active\nAda,36,true\nGrace,41,false\n", path =>
        {
            var schema = new SeparatedValuesSchema();
            var columns = new ISchemaColumn[]
            {
                new SchemaColumn("Active", 0, typeof(bool)),
                new SchemaColumn("Name", 1, typeof(string)),
                new SchemaColumn("Age", 2, typeof(long))
            };
            var source = CreateQuerySource<QueryStructRow, QueryStructMaterializer>(
                schema,
                path,
                columns,
                ["Active", "Name", "Age"],
                new QueryRowShape(
                [
                    new QueryRowField(0, 0, "Active", typeof(bool), false),
                    new QueryRowField(1, 1, "Name", typeof(string), true),
                    new QueryRowField(2, 2, "Age", typeof(long), false)
                ]));

            var rows = source.Chunks.SelectMany(static chunk => chunk).ToArray();

            CollectionAssert.AreEqual(
                new[]
                {
                    new QueryStructRow(true, "Ada", 36),
                    new QueryStructRow(false, "Grace", 41)
                },
                rows);
        });
    }

    [TestMethod]
    public void QuerySource_WhenStringsAreEmptyOrNull_PreservesQuotedEmptyForClassCarrier()
    {
        WithCsv("Name,Age\n\"\",1\n,2\n", path =>
        {
            var schema = new SeparatedValuesSchema();
            var columns = new ISchemaColumn[]
            {
                new SchemaColumn("Name", 0, typeof(string)),
                new SchemaColumn("Age", 1, typeof(long))
            };
            var source = CreateQuerySource<QueryClassRow, QueryClassMaterializer>(
                schema,
                path,
                columns,
                ["Name", "Age"],
                new QueryRowShape(
                [
                    new QueryRowField(0, 0, "Name", typeof(string), true),
                    new QueryRowField(1, 1, "Age", typeof(long), false)
                ]));

            var rows = source.Chunks.SelectMany(static chunk => chunk).ToArray();

            Assert.AreEqual(2, rows.Length);
            Assert.AreEqual(string.Empty, rows[0].Name);
            Assert.IsNull(rows[1].Name);
            Assert.AreEqual(1L, rows[0].Age);
            Assert.AreEqual(2L, rows[1].Age);
        });
    }

    [TestMethod]
    public void QuerySource_WhenShapeHasNoFields_MaterializesEveryAcceptedClassCarrier()
    {
        WithCsv("Name\nAda\nGrace\n", path =>
        {
            var schema = new SeparatedValuesSchema();
            var source = CreateQuerySource<ZeroFieldRow, ZeroFieldMaterializer>(
                schema,
                path,
                [],
                [],
                new QueryRowShape([]));

            var rows = source.Chunks.SelectMany(static chunk => chunk).ToArray();

            Assert.AreEqual(2, rows.Length);
            Assert.AreNotSame(rows[0], rows[1]);
        });
    }

    [TestMethod]
    public void QuerySource_AppliesPredicateSkipAndTakeBeforeMaterialization()
    {
        WithCsv("Name,Score\nA,1\nB,2\nC,3\nD,4\n", path =>
        {
            var schema = new SeparatedValuesSchema();
            var identity = CreateIdentity();
            var metadata = MetadataContext([]);
            var descriptor = schema.DescribeSource(
                "comma",
                new SourceDescribeContext(identity, metadata),
                path,
                true,
                0);
            Assert.AreEqual(
                SourceTransferCapabilities.QueryScopedRows | SourceTransferCapabilities.LogicalScalarReads,
                descriptor.TransferCapabilities);
            var predicate = new SourcePredicateComparison(
                SourcePredicateComparisonOperator.GreaterThan,
                new SourcePredicateColumn(new SourceColumnRef("Score")),
                new SourcePredicateLiteral(1L));
            var plan = schema.TryPlanSource(
                "comma",
                new SourcePlanRequest
                {
                    Identity = identity,
                    RequiredColumns = [new SourceColumnRef("Name")],
                    SourceRuntimeSettings = new Dictionary<string, string>(),
                    Predicate = predicate,
                    OrderBy = [],
                    Skip = 1,
                    Take = 1
                },
                path,
                true,
                0).ExecutionPlan;
            Assert.AreEqual(predicate, plan.AcceptedPredicate);
            var columns = new ISchemaColumn[] { new SchemaColumn("Name", 0, typeof(string)) };
            var context = RuntimeV2TestContexts.CreateExecutionContext(
                CancellationToken.None,
                columns,
                executionPlan: plan);
            var shape = new QueryRowShape(
                [new QueryRowField(0, 0, "Name", typeof(string), true)]);
            var source = ((IQueryScopedRowSourceSchema)schema)
                .GetQueryScopedRowSource<string, OnlyCMaterializer>(
                    "comma",
                    new QueryScopedRowSourceRequest(context, shape),
                    path,
                    true,
                    0);

            CollectionAssert.AreEqual(
                new[] { "C" },
                source.Chunks.SelectMany(static chunk => chunk).ToArray());
        });
    }

    [TestMethod]
    public void LegacySource_WhenPlannerSelectsIt_ThrowsBeforeFileAccessEvenWhenCancelled()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var schema = new SeparatedValuesSchema();
        var context = RuntimeV2TestContexts.CreateExecutionContext(
            cancellation.Token,
            executionPlan: SourceExecutionPlan.Empty(CreateIdentity()));

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            schema.GetRowSource<object?[]>(
                "comma",
                context,
                Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.csv"),
                true,
                0));

        StringAssert.Contains(exception.Message, "separatedvalues.comma");
        StringAssert.Contains(exception.Message, "source:query-row-test");
        StringAssert.Contains(exception.Message, typeof(object[]).ToString());
        StringAssert.Contains(exception.Message, "core planner selected unsupported legacy row transfer");
    }

    [TestMethod]
    public void QuerySource_WhenRuntimeShapeTypeDiffers_ThrowsWithoutLegacyRetry()
    {
        WithCsv("Name\nAda\n", path =>
        {
            var shape = new QueryRowShape(
                [new QueryRowField(0, 0, "Name", typeof(long), false)]);
            var source = CreateQuerySource<long, OnlyLongMaterializer>(
                new SeparatedValuesSchema(),
                path,
                [new SchemaColumn("Name", 0, typeof(string))],
                ["Name"],
                shape,
                MaximumParallelism("1"));

            var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
                source.Chunks.SelectMany(static chunk => chunk).ToArray());

            StringAssert.Contains(exception.Message, "separatedvalues.comma");
            StringAssert.Contains(exception.Message, "source:query-row-test");
            StringAssert.Contains(exception.Message, shape.Fingerprint);
            StringAssert.Contains(exception.Message, "does not match planned type");
        });
    }

    [TestMethod]
    public void CompiledQuery_MaterializesGeneratedCarrier()
    {
        WithCsv("Name,Age\nAda,36\nGrace,41\n", path =>
        {
            var query = $"select Age, Name from separatedvalues.comma('{QueryPath(path)}', true, 0)";
            var compiled = InstanceCreatorHelpers.CompileForExecution(
                query,
                $"SeparatedValuesQueryRows_{Guid.NewGuid():N}",
                new CsvSchemaProvider(),
                EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());

            var table = compiled.Run();

            Assert.AreEqual(2, table.Count);
            Assert.AreEqual(36L, table[0][0]);
            Assert.AreEqual("Ada", table[0][1]);
            Assert.AreEqual(41L, table[1][0]);
            Assert.AreEqual("Grace", table[1][1]);
        });
    }

    [TestMethod]
    public void QuerySource_WhenParallelismIsForced_MatchesSequentialRowsAcrossChunkBoundaries()
    {
        var contents = new StringBuilder("Name,Age\n");
        for (var row = 0; row < 5_000; row++)
        {
            var name = row % 97 == 0 ? $"\"row-{row},quoted\"" : $"row-{row}";
            contents.Append(name).Append(',').Append(row).Append('\n');
        }

        WithCsv(contents.ToString(), path =>
        {
            var columns = new ISchemaColumn[]
            {
                new SchemaColumn("Name", 0, typeof(string)),
                new SchemaColumn("Age", 1, typeof(long))
            };
            var shape = new QueryRowShape(
            [
                new QueryRowField(0, 0, "Name", typeof(string), true),
                new QueryRowField(1, 1, "Age", typeof(long), false)
            ]);
            var sequential = CreateQuerySource<NameAgeRow, NameAgeMaterializer>(
                    new SeparatedValuesSchema(),
                    path,
                    columns,
                    ["Name", "Age"],
                    shape,
                    MaximumParallelism("1"))
                .Chunks
                .SelectMany(static chunk => chunk)
                .ToArray();
            var budget = new SeparatedValuesOutputMemoryBudget(256 * 1024, 1024);
            var parallelSchema = CreateSchemaWithParallelPipeline(budget, blockSize: 257);
            var parallelChunks = CreateQuerySource<NameAgeRow, NameAgeMaterializer>(
                    parallelSchema,
                    path,
                    columns,
                    ["Name", "Age"],
                    shape,
                    MaximumParallelism("4"))
                .Chunks
                .Select(static chunk => chunk.ToArray())
                .ToArray();
            var parallel = parallelChunks.SelectMany(static chunk => chunk).ToArray();

            CollectionAssert.AreEqual(sequential, parallel);
            Assert.IsTrue(parallelChunks.Length > 1);
            for (var chunk = 1; chunk < parallelChunks.Length; chunk++)
            {
                Assert.AreEqual(
                    parallelChunks[chunk - 1][^1].Age + 1,
                    parallelChunks[chunk][0].Age,
                    $"chunk boundary {chunk}");
            }
            Assert.IsTrue(budget.HighWaterBytes > 0);
            Assert.AreEqual(0L, budget.CurrentReservedBytes);
            AssertAllProcessWideLeasesReturned();
        });
    }

    [TestMethod]
    public void QuerySource_WhenParallelWorkerConversionFails_ReturnsEveryOutputLease()
    {
        var contents = new StringBuilder("Name,Age\n");
        for (var row = 0; row < 2_000; row++)
            contents.Append("row-").Append(row).Append(',').Append(row).Append('\n');
        contents.Append("bad,not-a-number\n");

        WithCsv(contents.ToString(), path =>
        {
            var budget = new SeparatedValuesOutputMemoryBudget(128 * 1024, 1024);
            var schema = CreateSchemaWithParallelPipeline(budget, blockSize: 127);
            var columns = NameAgeColumns();
            var source = CreateQuerySource<NameAgeRow, NameAgeMaterializer>(
                schema,
                path,
                columns,
                ["Name", "Age"],
                NameAgeShape(),
                MaximumParallelism("4"));

            var exception = Assert.ThrowsExactly<FormatException>(() =>
                source.Chunks.SelectMany(static chunk => chunk).ToArray());

            StringAssert.Contains(FlattenMessages(exception), "column 'Age'");
            Assert.AreEqual(0L, budget.CurrentReservedBytes);
            AssertAllProcessWideLeasesReturned();
        });
    }

    [TestMethod]
    public void QuerySource_WhenParallelScanIsCancelled_ReturnsEveryOutputLease()
    {
        var contents = new StringBuilder("Name,Age\n");
        for (var row = 0; row < 20_000; row++)
            contents.Append("row-").Append(row).Append(',').Append(row).Append('\n');

        WithCsv(contents.ToString(), path =>
        {
            using var cancellation = new CancellationTokenSource();
            var budget = new SeparatedValuesOutputMemoryBudget(128 * 1024, 1024);
            var schema = CreateSchemaWithParallelPipeline(budget, blockSize: 257);
            var progress = new DataSourceProgressCapture();
            var diagnosticsSink = new RecordingDiagnosticsSink();
            DataSourceEventHandler callback = (sender, args) =>
            {
                progress.Handler(sender, args);
                if (args.Phase == DataSourcePhase.RowsRead)
                    cancellation.Cancel();
            };
            var source = CreateQuerySource<NameAgeRow, NameAgeMaterializer>(
                schema,
                path,
                NameAgeColumns(),
                ["Name", "Age"],
                NameAgeShape(),
                MaximumParallelism("4"),
                cancellation.Token,
                callback,
                sourceDiagnostics: new SourceDiagnostics(diagnosticsSink));

            Assert.ThrowsExactly<OperationCanceledException>(() =>
                source.Chunks.SelectMany(static chunk => chunk).ToArray());
            Assert.AreEqual(1L, diagnosticsSink.Metric(SeparatedValuesDiagnostics.ExecutionCancellations));
            Assert.AreEqual(0L, diagnosticsSink.Metric(SeparatedValuesDiagnostics.ExecutionFailures));
            Assert.AreEqual(1, progress.For("separated_values", DataSourcePhase.Begin).Count);
            Assert.AreEqual(1, progress.For("separated_values", DataSourcePhase.End).Count);
            Assert.AreEqual(0L, budget.CurrentReservedBytes);
            AssertAllProcessWideLeasesReturned();
        });
    }

    [TestMethod]
    public void QuerySource_WhenParallelConsumerAbandonsEnumeration_ReturnsEveryOutputLease()
    {
        var contents = new StringBuilder("Name,Age\n");
        for (var row = 0; row < 20_000; row++)
            contents.Append("row-").Append(row).Append(',').Append(row).Append('\n');

        WithCsv(contents.ToString(), path =>
        {
            var budget = new SeparatedValuesOutputMemoryBudget(128 * 1024, 1024);
            var schema = CreateSchemaWithParallelPipeline(budget, blockSize: 257);
            var progress = new DataSourceProgressCapture();
            var source = CreateQuerySource<NameAgeRow, NameAgeMaterializer>(
                schema,
                path,
                NameAgeColumns(),
                ["Name", "Age"],
                NameAgeShape(),
                MaximumParallelism("4"),
                dataSourceProgressCallback: progress.Handler);

            using (var chunks = source.Chunks.GetEnumerator())
                Assert.IsTrue(chunks.MoveNext());

            Assert.IsTrue(
                SpinWait.SpinUntil(() => budget.CurrentReservedBytes == 0, TimeSpan.FromSeconds(5)),
                $"reserved={budget.CurrentReservedBytes:N0}");
            Assert.IsTrue(
                SpinWait.SpinUntil(
                    () => progress.For("separated_values", DataSourcePhase.End).Count == 1,
                    TimeSpan.FromSeconds(5)));
            Assert.AreEqual(1, progress.For("separated_values", DataSourcePhase.Begin).Count);
            AssertAllProcessWideLeasesReturned();
        });
    }

    [TestMethod]
    public void QueryOutputEstimator_StoresOnlyNumericAccountingAndDistinguishesCarrierCategories()
    {
        var shape = NameAgeShape();

        var structEstimator = SeparatedValuesQueryOutputMemoryEstimator.Create<NameAgeRow>(shape);
        var classEstimator = SeparatedValuesQueryOutputMemoryEstimator.Create<QueryClassRow>(shape);

        Assert.IsTrue(structEstimator.Estimate(100, 1_000) > 2_000);
        Assert.IsTrue(classEstimator.Estimate(100, 1_000) > structEstimator.Estimate(100, 1_000));
        Assert.IsTrue(typeof(SeparatedValuesQueryOutputMemoryEstimator)
            .GetFields(System.Reflection.BindingFlags.Instance |
                       System.Reflection.BindingFlags.Public |
                       System.Reflection.BindingFlags.NonPublic)
            .All(static field => field.FieldType != typeof(Type) &&
                                 !typeof(Delegate).IsAssignableFrom(field.FieldType)));
    }

    [TestMethod]
    public void QuerySource_AllSupportedNullableTypes_PreserveValuesAndNullTokens()
    {
        const string header =
            "Text,Boolean,Byte,SByte,Int16,Int32,Int64,UInt16,UInt32,UInt64,Single,Double,Decimal,Character,DateTime,DateTimeOffset,DateOnly,TimeOnly,TimeSpan,Guid";
        const string values =
            "value,true,255,-12,-32000,-2000000000,-900000000000,65000,4000000000,18000000000000000000,1.25,-2.5,123.45,Z,2026-08-19 10:20:30,2026-08-19T10:20:30+02:00,2026-08-19,10:20:30,01:02:03,01234567-89ab-cdef-0123-456789abcdef";
        var nulls = new string(',', 19);

        WithCsv($"{header}\n{values}\n{nulls}\n", path =>
        {
            var columns = SupportedNullableColumns();
            var shape = new QueryRowShape(columns
                .Select((column, index) => new QueryRowField(
                    index,
                    index,
                    column.ColumnName,
                    column.ColumnType,
                    true))
                .ToArray());
            var source = CreateQuerySource<SupportedTypesRow, SupportedTypesMaterializer>(
                new SeparatedValuesSchema(),
                path,
                columns,
                columns.Select(static column => column.ColumnName).ToArray(),
                shape,
                MaximumParallelism("1"));

            var rows = source.Chunks.SelectMany(static chunk => chunk).ToArray();

            Assert.AreEqual(2, rows.Length);
            Assert.AreEqual(new SupportedTypesRow(
                "value",
                true,
                byte.MaxValue,
                -12,
                -32000,
                -2000000000,
                -900000000000,
                65000,
                4000000000,
                18000000000000000000,
                1.25f,
                -2.5d,
                123.45m,
                'Z',
                new DateTime(2026, 8, 19, 10, 20, 30, DateTimeKind.Unspecified),
                new DateTimeOffset(2026, 8, 19, 10, 20, 30, TimeSpan.FromHours(2)),
                new DateOnly(2026, 8, 19),
                new TimeOnly(10, 20, 30),
                new TimeSpan(1, 2, 3),
                Guid.Parse("01234567-89ab-cdef-0123-456789abcdef")), rows[0]);
            Assert.AreEqual(SupportedTypesRow.Null, rows[1]);
        });
    }

    [TestMethod]
    public void QuerySource_WhenHeaderlessProjectionIsReordered_UsesPhysicalColumnOrdinals()
    {
        WithCsv("Ada,36,true\nGrace,41,false\n", path =>
        {
            var columns = new ISchemaColumn[]
            {
                new SchemaColumn("Column1", 0, typeof(string)),
                new SchemaColumn("Column2", 1, typeof(long)),
                new SchemaColumn("Column3", 2, typeof(bool))
            };
            var source = CreateQuerySource<HeaderlessRow, HeaderlessMaterializer>(
                new SeparatedValuesSchema(),
                path,
                columns,
                ["Column1", "Column2", "Column3"],
                new QueryRowShape(
                [
                    new QueryRowField(0, 2, "Column3", typeof(bool), false),
                    new QueryRowField(1, 0, "Column1", typeof(string), true)
                ]),
                MaximumParallelism("1"),
                hasHeader: false);

            CollectionAssert.AreEqual(
                new[] { new HeaderlessRow(true, "Ada"), new HeaderlessRow(false, "Grace") },
                source.Chunks.SelectMany(static chunk => chunk).ToArray());
        });
    }

    [TestMethod]
    public void QuerySource_WhenShortRecordOmitsNonNullableField_FailsWithSourceAndShapeContext()
    {
        WithCsv("Name,Age\nAda\n", path =>
        {
            var progress = new DataSourceProgressCapture();
            var diagnosticsSink = new RecordingDiagnosticsSink();
            var source = CreateQuerySource<NameAgeRow, NameAgeMaterializer>(
                new SeparatedValuesSchema(),
                path,
                NameAgeColumns(),
                ["Name", "Age"],
                NameAgeShape(),
                MaximumParallelism("1"),
                dataSourceProgressCallback: progress.Handler,
                sourceDiagnostics: new SourceDiagnostics(diagnosticsSink));

            var exception = Assert.ThrowsExactly<FormatException>(() =>
                source.Chunks.SelectMany(static chunk => chunk).ToArray());

            StringAssert.Contains(exception.Message, Path.GetFileName(path));
            StringAssert.Contains(exception.Message, "column 'Age' is missing");
            StringAssert.Contains(exception.Message, NameAgeShape().Fingerprint);
            StringAssert.Contains(exception.Message, "non-nullable 'System.Int64'");
            Assert.AreEqual(1L, diagnosticsSink.Metric(SeparatedValuesDiagnostics.ExecutionFailures));
            Assert.AreEqual(0L, diagnosticsSink.Metric(SeparatedValuesDiagnostics.ExecutionCancellations));
            Assert.AreEqual(1, progress.For("separated_values", DataSourcePhase.Begin).Count);
            Assert.AreEqual(1, progress.For("separated_values", DataSourcePhase.End).Count);
        });
    }

    [TestMethod]
    public void QuerySource_WhenHeaderHasUnicodePunctuationAndQualifiedShapeNames_BindsExactly()
    {
        WithCsv("\"Full, Name\",naïve-value!\nAda,ready\n", path =>
        {
            var columns = new ISchemaColumn[]
            {
                new SchemaColumn("Full, Name", 0, typeof(string)),
                new SchemaColumn("naïve-value!", 1, typeof(string))
            };
            var source = CreateQuerySource<SpecialNameRow, SpecialNameMaterializer>(
                new SeparatedValuesSchema(),
                path,
                columns,
                ["Full, Name", "naïve-value!"],
                new QueryRowShape(
                [
                    new QueryRowField(0, 0, "csv.Full, Name", typeof(string), true),
                    new QueryRowField(1, 1, "csv.naïve-value!", typeof(string), true)
                ]),
                MaximumParallelism("1"));

            Assert.AreEqual(
                new SpecialNameRow("Ada", "ready"),
                source.Chunks.SelectMany(static chunk => chunk).Single());
        });
    }

    [TestMethod]
    public void QuerySource_WhenExecutionIsPreCancelled_DoesNotOpenSourceAndReportsCancellationOnce()
    {
        WithCsv("Name,Age\nAda,36\n", path =>
        {
            var schema = new SeparatedValuesSchema();
            var columns = NameAgeColumns();
            var plan = CreatePlan(schema, path, columns, ["Name", "Age"]);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var progress = new DataSourceProgressCapture();
            var diagnosticsSink = new RecordingDiagnosticsSink();
            var context = RuntimeV2TestContexts.CreateExecutionContext(
                cancellation.Token,
                columns,
                dataSourceProgressCallback: progress.Handler,
                executionPlan: plan,
                sourceDiagnostics: new SourceDiagnostics(diagnosticsSink));
            var source = ((IQueryScopedRowSourceSchema)schema)
                .GetQueryScopedRowSource<NameAgeRow, NameAgeMaterializer>(
                    "comma",
                    new QueryScopedRowSourceRequest(context, NameAgeShape()),
                    path,
                    true,
                    0);
            File.Delete(path);

            Assert.AreEqual(0, source.Chunks.SelectMany(static chunk => chunk).Count());
            Assert.AreEqual(1L, diagnosticsSink.Metric(SeparatedValuesDiagnostics.ExecutionCancellations));
            Assert.AreEqual(0L, diagnosticsSink.Metric(SeparatedValuesDiagnostics.ExecutionFailures));
            Assert.AreEqual(1, progress.For("separated_values", DataSourcePhase.Begin).Count);
            Assert.AreEqual(1, progress.For("separated_values", DataSourcePhase.End).Count);
        });
    }

    [TestMethod]
    public void QuerySource_WhenSourceOpenFails_PreservesFailureAndReportsLifecycleOnce()
    {
        WithCsv("Name,Age\nAda,36\n", path =>
        {
            var schema = new SeparatedValuesSchema();
            var columns = NameAgeColumns();
            var plan = CreatePlan(schema, path, columns, ["Name", "Age"]);
            var progress = new DataSourceProgressCapture();
            var diagnosticsSink = new RecordingDiagnosticsSink();
            var context = RuntimeV2TestContexts.CreateExecutionContext(
                CancellationToken.None,
                columns,
                dataSourceProgressCallback: progress.Handler,
                executionPlan: plan,
                sourceDiagnostics: new SourceDiagnostics(diagnosticsSink));
            var source = ((IQueryScopedRowSourceSchema)schema)
                .GetQueryScopedRowSource<NameAgeRow, NameAgeMaterializer>(
                    "comma",
                    new QueryScopedRowSourceRequest(context, NameAgeShape()),
                    path,
                    true,
                    0);
            File.Delete(path);

            var exception = Assert.ThrowsExactly<FileNotFoundException>(() =>
                source.Chunks.SelectMany(static chunk => chunk).ToArray());

            StringAssert.Contains(exception.Message, Path.GetFileName(path));
            Assert.AreEqual(1L, diagnosticsSink.Metric(SeparatedValuesDiagnostics.ExecutionFailures));
            Assert.AreEqual(0L, diagnosticsSink.Metric(SeparatedValuesDiagnostics.ExecutionCancellations));
            Assert.AreEqual(1, progress.For("separated_values", DataSourcePhase.Begin).Count);
            Assert.AreEqual(1, progress.For("separated_values", DataSourcePhase.End).Count);
            AssertAllProcessWideLeasesReturned();
        });
    }

    [TestMethod]
    public void QuerySource_WhenMaterializerFails_PreservesInnerExceptionAndReportsFailureOnce()
    {
        WithCsv("Name\nAda\n", path =>
        {
            var progress = new DataSourceProgressCapture();
            var diagnosticsSink = new RecordingDiagnosticsSink();
            var source = CreateQuerySource<string, ThrowingMaterializer>(
                new SeparatedValuesSchema(),
                path,
                [new SchemaColumn("Name", 0, typeof(string))],
                ["Name"],
                new QueryRowShape([new QueryRowField(0, 0, "Name", typeof(string), true)]),
                MaximumParallelism("1"),
                dataSourceProgressCallback: progress.Handler,
                sourceDiagnostics: new SourceDiagnostics(diagnosticsSink));

            var exception = Assert.ThrowsExactly<MaterializerFailureException>(() =>
                source.Chunks.SelectMany(static chunk => chunk).ToArray());

            Assert.IsInstanceOfType<InvalidOperationException>(exception.InnerException);
            Assert.AreEqual("inner-marker", exception.InnerException.Message);
            Assert.AreEqual(1L, diagnosticsSink.Metric(SeparatedValuesDiagnostics.ExecutionFailures));
            Assert.AreEqual(0L, diagnosticsSink.Metric(SeparatedValuesDiagnostics.ExecutionCancellations));
            Assert.AreEqual(1, progress.For("separated_values", DataSourcePhase.Begin).Count);
            Assert.AreEqual(1, progress.For("separated_values", DataSourcePhase.End).Count);
            AssertAllProcessWideLeasesReturned();
        });
    }

    private static RowSource<TRow> CreateQuerySource<TRow, TMaterializer>(
        SeparatedValuesSchema schema,
        string path,
        IReadOnlyCollection<ISchemaColumn> columns,
        IReadOnlyList<string> requiredColumns,
        QueryRowShape shape,
        IReadOnlyDictionary<string, string>? runtimeSettings = null,
        CancellationToken cancellationToken = default,
        DataSourceEventHandler? dataSourceProgressCallback = null,
        bool hasHeader = true,
        int skipLines = 0,
        SourceDiagnostics? sourceDiagnostics = null)
        where TMaterializer : struct, IQueryRowMaterializer<TRow>
    {
        var identity = CreateIdentity();
        runtimeSettings ??= new Dictionary<string, string>();
        var metadata = MetadataContext(columns, runtimeSettings, cancellationToken);
        var descriptor = schema.DescribeSource(
            "comma",
            new SourceDescribeContext(identity, metadata),
            path,
            hasHeader,
            skipLines);
        Assert.AreEqual(
            SourceTransferCapabilities.QueryScopedRows | SourceTransferCapabilities.LogicalScalarReads,
            descriptor.TransferCapabilities);
        var plan = schema.TryPlanSource(
            "comma",
            new SourcePlanRequest
            {
                Identity = identity,
                RequiredColumns = requiredColumns.Select(static name => new SourceColumnRef(name)).ToArray(),
                SourceRuntimeSettings = runtimeSettings,
                Predicate = null,
                OrderBy = [],
                Skip = null,
                Take = null
            },
            path,
            hasHeader,
            skipLines).ExecutionPlan;
        var executionContext = RuntimeV2TestContexts.CreateExecutionContext(
            cancellationToken,
            columns,
            runtimeSettings,
            dataSourceProgressCallback: dataSourceProgressCallback,
            executionPlan: plan,
            sourceDiagnostics: sourceDiagnostics);

        return ((IQueryScopedRowSourceSchema)schema)
            .GetQueryScopedRowSource<TRow, TMaterializer>(
                "comma",
                new QueryScopedRowSourceRequest(executionContext, shape),
                path,
                hasHeader,
                skipLines);
    }

    private static SourceMetadataContext MetadataContext(
        IReadOnlyCollection<ISchemaColumn> columns,
        IReadOnlyDictionary<string, string>? runtimeSettings = null,
        CancellationToken cancellationToken = default)
    {
        return new SourceMetadataContext(
            "query-row-test",
            cancellationToken,
            columns,
            runtimeSettings ?? new Dictionary<string, string>(),
            new Mock<ILogger>().Object);
    }

    private static SourceExecutionPlan CreatePlan(
        SeparatedValuesSchema schema,
        string path,
        IReadOnlyCollection<ISchemaColumn> columns,
        IReadOnlyList<string> requiredColumns)
    {
        var identity = CreateIdentity();
        schema.DescribeSource(
            "comma",
            new SourceDescribeContext(identity, MetadataContext(columns)),
            path,
            true,
            0);
        return schema.TryPlanSource(
            "comma",
            new SourcePlanRequest
            {
                Identity = identity,
                RequiredColumns = requiredColumns.Select(static name => new SourceColumnRef(name)).ToArray(),
                SourceRuntimeSettings = new Dictionary<string, string>(),
                Predicate = null,
                OrderBy = [],
                Skip = null,
                Take = null
            },
            path,
            true,
            0).ExecutionPlan;
    }

    private static SeparatedValuesSchema CreateSchemaWithParallelPipeline(
        ISeparatedValuesOutputMemoryBudget outputMemoryBudget,
        int blockSize)
    {
        var scanPipeline = new SeparatedValuesScanPipeline(
            new SeparatedValuesParallelBlockScanPipeline(
                blockSize: blockSize,
                outputMemoryBudget: outputMemoryBudget),
            forceParallel: true);
        return new SeparatedValuesSchema(
            new SeparatedValuesPipelineModules(
                new BoundedSeparatedValuesSchemaResolver(),
                scanPipeline));
    }

    private static IReadOnlyDictionary<string, string> MaximumParallelism(string value) =>
        new Dictionary<string, string>
        {
            [SeparatedValuesParallelScanOptions.MaximumParallelismSettingName] = value
        };

    private static ISchemaColumn[] NameAgeColumns() =>
    [
        new SchemaColumn("Name", 0, typeof(string)),
        new SchemaColumn("Age", 1, typeof(long))
    ];

    private static ISchemaColumn[] SupportedNullableColumns() =>
    [
        new SchemaColumn("Text", 0, typeof(string)),
        new SchemaColumn("Boolean", 1, typeof(bool?)),
        new SchemaColumn("Byte", 2, typeof(byte?)),
        new SchemaColumn("SByte", 3, typeof(sbyte?)),
        new SchemaColumn("Int16", 4, typeof(short?)),
        new SchemaColumn("Int32", 5, typeof(int?)),
        new SchemaColumn("Int64", 6, typeof(long?)),
        new SchemaColumn("UInt16", 7, typeof(ushort?)),
        new SchemaColumn("UInt32", 8, typeof(uint?)),
        new SchemaColumn("UInt64", 9, typeof(ulong?)),
        new SchemaColumn("Single", 10, typeof(float?)),
        new SchemaColumn("Double", 11, typeof(double?)),
        new SchemaColumn("Decimal", 12, typeof(decimal?)),
        new SchemaColumn("Character", 13, typeof(char?)),
        new SchemaColumn("DateTime", 14, typeof(DateTime?)),
        new SchemaColumn("DateTimeOffset", 15, typeof(DateTimeOffset?)),
        new SchemaColumn("DateOnly", 16, typeof(DateOnly?)),
        new SchemaColumn("TimeOnly", 17, typeof(TimeOnly?)),
        new SchemaColumn("TimeSpan", 18, typeof(TimeSpan?)),
        new SchemaColumn("Guid", 19, typeof(Guid?))
    ];

    private static QueryRowShape NameAgeShape() => new(
    [
        new QueryRowField(0, 0, "Name", typeof(string), true),
        new QueryRowField(1, 1, "Age", typeof(long), false)
    ]);

    private static string FlattenMessages(Exception exception)
    {
        var messages = string.Empty;
        for (var current = exception; current is not null; current = current.InnerException)
            messages += current.Message + Environment.NewLine;
        return messages;
    }

    private static void AssertAllProcessWideLeasesReturned()
    {
        Assert.IsTrue(
            SpinWait.SpinUntil(
                () => SeparatedValuesStructuralMemoryBudget.CurrentReservedBytes == 0 &&
                      SeparatedValuesStructuralMemoryBudget.CurrentReadAheadReservedBytes == 0 &&
                      SeparatedValuesCpuBudget.CurrentLeases == 0,
                TimeSpan.FromSeconds(5)),
            $"structural={SeparatedValuesStructuralMemoryBudget.CurrentReservedBytes:N0}, " +
            $"read-ahead={SeparatedValuesStructuralMemoryBudget.CurrentReadAheadReservedBytes:N0}, " +
            $"cpu={SeparatedValuesCpuBudget.CurrentLeases:N0}");
    }

    private static SourceIdentity CreateIdentity() =>
        new("separatedvalues", "comma", "source:query-row-test", "items");

    private static string QueryPath(string path)
    {
        return Path.GetFullPath(path)
            .Replace('\\', '/')
            .Replace("'", "''", StringComparison.Ordinal);
    }

    private static void WithCsv(string contents, Action<string> assertion)
    {
        var path = Path.Combine(Path.GetTempPath(), $"musoq-query-row-{Guid.NewGuid():N}.csv");
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

    private readonly record struct QueryStructRow(bool Active, string? Name, long Age);

    private readonly struct QueryStructMaterializer : IQueryRowMaterializer<QueryStructRow>
    {
        public static QueryStructRow Materialize<TReader>(scoped ref TReader reader)
            where TReader : IQuerySourceFieldReader, allows ref struct
        {
            return new QueryStructRow(
                reader.Read<bool>(0),
                reader.Read<string?>(1),
                reader.Read<long>(2));
        }
    }

    private sealed record QueryClassRow(string? Name, long Age);

    private readonly record struct NameAgeRow(string? Name, long Age);

    private readonly struct NameAgeMaterializer : IQueryRowMaterializer<NameAgeRow>
    {
        public static NameAgeRow Materialize<TReader>(scoped ref TReader reader)
            where TReader : IQuerySourceFieldReader, allows ref struct
        {
            return new NameAgeRow(reader.Read<string?>(0), reader.Read<long>(1));
        }
    }

    private readonly struct QueryClassMaterializer : IQueryRowMaterializer<QueryClassRow>
    {
        public static QueryClassRow Materialize<TReader>(scoped ref TReader reader)
            where TReader : IQuerySourceFieldReader, allows ref struct
        {
            return new QueryClassRow(reader.Read<string?>(0), reader.Read<long>(1));
        }
    }

    private sealed class ZeroFieldRow;

    private readonly struct ZeroFieldMaterializer : IQueryRowMaterializer<ZeroFieldRow>
    {
        public static ZeroFieldRow Materialize<TReader>(scoped ref TReader reader)
            where TReader : IQuerySourceFieldReader, allows ref struct
        {
            return new ZeroFieldRow();
        }
    }

    private readonly struct OnlyCMaterializer : IQueryRowMaterializer<string>
    {
        public static string Materialize<TReader>(scoped ref TReader reader)
            where TReader : IQuerySourceFieldReader, allows ref struct
        {
            var value = reader.Read<string>(0);
            return value == "C"
                ? value
                : throw new InvalidOperationException($"Unexpected materialization of '{value}'.");
        }
    }

    private readonly struct OnlyLongMaterializer : IQueryRowMaterializer<long>
    {
        public static long Materialize<TReader>(scoped ref TReader reader)
            where TReader : IQuerySourceFieldReader, allows ref struct => reader.Read<long>(0);
    }

    private readonly record struct HeaderlessRow(bool Active, string? Name);

    private readonly struct HeaderlessMaterializer : IQueryRowMaterializer<HeaderlessRow>
    {
        public static HeaderlessRow Materialize<TReader>(scoped ref TReader reader)
            where TReader : IQuerySourceFieldReader, allows ref struct =>
            new(reader.Read<bool>(0), reader.Read<string?>(1));
    }

    private readonly record struct SpecialNameRow(string? Name, string? Status);

    private readonly struct SpecialNameMaterializer : IQueryRowMaterializer<SpecialNameRow>
    {
        public static SpecialNameRow Materialize<TReader>(scoped ref TReader reader)
            where TReader : IQuerySourceFieldReader, allows ref struct =>
            new(reader.Read<string?>(0), reader.Read<string?>(1));
    }

    private readonly record struct SupportedTypesRow(
        string? Text,
        bool? Boolean,
        byte? Byte,
        sbyte? SByte,
        short? Int16,
        int? Int32,
        long? Int64,
        ushort? UInt16,
        uint? UInt32,
        ulong? UInt64,
        float? Single,
        double? Double,
        decimal? Decimal,
        char? Character,
        DateTime? DateTime,
        DateTimeOffset? DateTimeOffset,
        DateOnly? DateOnly,
        TimeOnly? TimeOnly,
        TimeSpan? TimeSpan,
        Guid? Guid)
    {
        public static SupportedTypesRow Null => new(
            null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null, null, null);
    }

    private readonly struct SupportedTypesMaterializer : IQueryRowMaterializer<SupportedTypesRow>
    {
        public static SupportedTypesRow Materialize<TReader>(scoped ref TReader reader)
            where TReader : IQuerySourceFieldReader, allows ref struct =>
            new(
                reader.Read<string?>(0),
                reader.Read<bool?>(1),
                reader.Read<byte?>(2),
                reader.Read<sbyte?>(3),
                reader.Read<short?>(4),
                reader.Read<int?>(5),
                reader.Read<long?>(6),
                reader.Read<ushort?>(7),
                reader.Read<uint?>(8),
                reader.Read<ulong?>(9),
                reader.Read<float?>(10),
                reader.Read<double?>(11),
                reader.Read<decimal?>(12),
                reader.Read<char?>(13),
                reader.Read<DateTime?>(14),
                reader.Read<DateTimeOffset?>(15),
                reader.Read<DateOnly?>(16),
                reader.Read<TimeOnly?>(17),
                reader.Read<TimeSpan?>(18),
                reader.Read<Guid?>(19));
    }

    private readonly struct ThrowingMaterializer : IQueryRowMaterializer<string>
    {
        public static string Materialize<TReader>(scoped ref TReader reader)
            where TReader : IQuerySourceFieldReader, allows ref struct
        {
            _ = reader.Read<string>(0);
            throw new MaterializerFailureException(
                "materializer-marker",
                new InvalidOperationException("inner-marker"));
        }
    }

    private sealed class MaterializerFailureException(string message, Exception innerException)
        : Exception(message, innerException);

    private sealed class RecordingDiagnosticsSink : ISourceDiagnosticsSink
    {
        private readonly Dictionary<string, long> _metrics = new(StringComparer.Ordinal);
        private readonly object _gate = new();

        public IDisposable Measure(string name, SourceDiagnosticOperation operation) => NoopDisposable.Instance;

        public void AddRowsProduced(long count)
        {
        }

        public void AddBytesRead(long bytes)
        {
        }

        public void AddMetric(string name, long value)
        {
            lock (_gate)
                _metrics[name] = Metric(name) + value;
        }

        public long Metric(string name)
        {
            lock (_gate)
                return _metrics.GetValueOrDefault(name);
        }

        private sealed class NoopDisposable : IDisposable
        {
            public static NoopDisposable Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }
}
