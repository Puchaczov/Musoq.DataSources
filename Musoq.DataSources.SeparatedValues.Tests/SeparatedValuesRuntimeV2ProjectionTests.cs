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
public class SeparatedValuesRuntimeV2ProjectionTests
{
    private const string PlanningFixture = "./Files/BankingTransactions.csv";

    [TestMethod]
    public void TryPlanSource_WhenRequiredColumnsAndSupportedPredicateArePresent_AcceptsProjectionPredicateAndSlice()
    {
        var schema = new SeparatedValuesSchema();
        var predicate = Equal("Name", "Alice");
        var request = CreateRequest(predicate, [new SourceColumnRef("Name")]);

        var result = schema.TryPlanSource("comma", request, PlanningFixture, true, 0);

        Assert.AreEqual(1, result.AcceptedColumns.Count);
        Assert.AreEqual("Name", result.AcceptedColumns[0].Name);
        Assert.AreEqual(predicate, result.AcceptedPredicate);
        Assert.IsNull(result.ResidualPredicate);
        Assert.AreEqual(request.Skip, result.AcceptedSkip);
        Assert.IsNull(result.ResidualSkip);
        Assert.AreEqual(request.Take, result.AcceptedTake);
        Assert.IsNull(result.ResidualTake);
    }

    [TestMethod]
    public void TryPlanSource_WhenStringOrderingPredicateIsPresent_KeepsPredicateResidual()
    {
        var predicate = GreaterThan("Name", "Alice");
        var request = CreateRequest(predicate, [new SourceColumnRef("Name")]);

        var result = new SeparatedValuesSchema().TryPlanSource(
            "comma",
            request,
            PlanningFixture,
            true,
            0);

        Assert.IsNull(result.AcceptedPredicate);
        Assert.AreEqual(predicate, result.ResidualPredicate);
        Assert.IsNull(result.AcceptedSkip);
        Assert.AreEqual(request.Skip, result.ResidualSkip);
    }

    [TestMethod]
    public void TryPlanSource_WhenNoColumnsAreRequired_AcceptsZeroColumnProjection()
    {
        var result = new SeparatedValuesSchema().TryPlanSource(
            "comma",
            CreateRequest(null, []),
            PlanningFixture,
            true,
            0);

        Assert.AreEqual(0, result.AcceptedColumns.Count);
        Assert.IsTrue(SeparatedValuesReadPlan.From(result.ExecutionPlan).ProjectionAccepted);
    }

    [TestMethod]
    public void TryPlanSource_WhenRequiredColumnsAreNull_DoesNotAcceptProjection()
    {
        var result = new SeparatedValuesSchema().TryPlanSource(
            "comma",
            CreateRequest(null, null),
            PlanningFixture,
            true,
            0);

        Assert.IsFalse(SeparatedValuesReadPlan.From(result.ExecutionPlan).ProjectionAccepted);
    }

    [TestMethod]
    public void TryPlanSource_WhenOrPredicateIsPresent_KeepsPredicateResidual()
    {
        var predicate = new SourcePredicateLogical(
            SourcePredicateLogicalOperator.Or,
            Equal("Name", "Alice"),
            Equal("Name", "Bob"));
        var request = CreateRequest(predicate, [new SourceColumnRef("Name")]);

        var result = new SeparatedValuesSchema().TryPlanSource(
            "comma",
            request,
            PlanningFixture,
            true,
            0);

        Assert.IsNull(result.AcceptedPredicate);
        Assert.AreEqual(predicate, result.ResidualPredicate);
        Assert.IsNull(result.AcceptedSkip);
        Assert.AreEqual(request.Skip, result.ResidualSkip);
    }

    [TestMethod]
    public void FileRowsSource_WhenProjectionIsAccepted_UsesDenseOutputAndSkipsUnrequestedConversion()
    {
        WithCsv("Name,Payload,Age\nAlice,unsupported,31\n", path =>
        {
            var plan = Plan(path, null, [new SourceColumnRef("Age")]);
            var context = RuntimeV2TestContexts.CreateExecutionContext(
                CancellationToken.None,
                [new SchemaColumn("Age", 0, typeof(long))],
                executionPlan: plan);
            var rows = ReadRows<long>(path, context);

            Assert.AreEqual(1, rows.Length);
            Assert.AreEqual(31L, rows[0].Item0);
        });
    }

    [TestMethod]
    public void FileRowsSource_WhenRuntimeProjectsLatePhysicalColumn_UsesDenseCarrierSlot()
    {
        var header = string.Join(',', Enumerable.Range(1, 100).Select(index => $"Column{index}"));
        var row = string.Join(',', Enumerable.Range(1, 100));
        WithCsv($"{header}\n{row}\n", path =>
        {
            var plan = Plan(path, null, [new SourceColumnRef("Column100")]);
            var context = RuntimeV2TestContexts.CreateExecutionContext(
                CancellationToken.None,
                [new SchemaColumn("Column100", 0, typeof(long))],
                executionPlan: plan);

            var rows = ReadRows<long>(path, context);

            Assert.AreEqual(1, rows.Length);
            Assert.AreEqual(100L, rows[0].Item0);
        });
    }

    [TestMethod]
    public void FileRowsSource_WhenExecutionPlanHasNoPhysicalLayout_RejectsTheShape()
    {
        WithCsv("A,B,C\n1,2,3\n", path =>
        {
            var plan = new SourceExecutionPlan
            {
                Identity = CreateIdentity(),
                AcceptedColumns = [new SourceColumnRef("C")],
                AcceptedOrderBy = []
            };
            var context = RuntimeV2TestContexts.CreateExecutionContext(
                CancellationToken.None,
                [new SchemaColumn("C", 0, typeof(long))],
                executionPlan: plan);
            var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
                ReadRows<long>(path, context));

            StringAssert.Contains(exception.Message, "no immutable physical-column layout");
        });
    }

    [TestMethod]
    public void FileRowsSource_WhenZeroColumnProjectionIsAccepted_EmitsEmptyRows()
    {
        WithCsv("Name,Age\nAlice,31\nBob,42\n", path =>
        {
            var plan = Plan(path, null, []);
            var context = RuntimeV2TestContexts.CreateExecutionContext(
                CancellationToken.None,
                [],
                executionPlan: plan);
            var chunks = SeparatedValuesNativeTestSource.Create(path, ",", true, 0, context)
                .Chunks
                .ToArray();
            var rows = chunks.SelectMany(chunk => chunk).ToArray();

            Assert.IsTrue(chunks.Length > 0);
            Assert.AreEqual(2, rows.Length);
        });
    }

    [TestMethod]
    public void FileRowsSource_WhenUnescapedStringRepeats_ReusesSnapshotStringInstance()
    {
        WithCsv("Name\nStation-A\nStation-A\n", path =>
        {
            var plan = Plan(path, null, [new SourceColumnRef("Name")]);
            var context = RuntimeV2TestContexts.CreateExecutionContext(
                CancellationToken.None,
                [new SchemaColumn("Name", 0, typeof(string))],
                executionPlan: plan);
            var rows = ReadRows<string>(path, context);

            Assert.AreEqual(2, rows.Length);
            Assert.AreSame(rows[0].Item0, rows[1].Item0);
        });
    }

    [TestMethod]
    public void FileRowsSource_WhenZeroColumnProjectionHasPredicate_StillParsesAndFiltersRows()
    {
        WithCsv("Name,Age\nAlice,31\nBob,42\nCarol,53\n", path =>
        {
            var plan = Plan(path, GreaterThan("Age", 40L), []);
            var context = RuntimeV2TestContexts.CreateExecutionContext(
                CancellationToken.None,
                [],
                executionPlan: plan);
            var rows = ReadRows(path, context);

            Assert.AreEqual(2, rows.Length);
        });
    }

    [TestMethod]
    public void FileRowsSource_WhenAcceptedPredicateUsesUnprojectedColumn_FiltersBeforeProjection()
    {
        WithCsv("Name,Age\nAlice,31\nBob,42\nCarol,53\n", path =>
        {
            var plan = Plan(
                path,
                GreaterThan("Age", 40L),
                [new SourceColumnRef("Name")]);
            var context = RuntimeV2TestContexts.CreateExecutionContext(
                CancellationToken.None,
                [new SchemaColumn("Name", 0, typeof(string))],
                executionPlan: plan);
            var rows = ReadRows<string>(path, context);

            CollectionAssert.AreEqual(new[] { "Bob", "Carol" }, rows.Select(row => row.Item0).ToArray());
        });
    }

    [TestMethod]
    public void FileRowsSource_WhenStringPredicateContainsEscapedQuote_MatchesWithoutMaterializingPredicateField()
    {
        WithCsv("Name,Payload\n\"a\"\"b\",first\nother,second\n", path =>
        {
            var plan = Plan(path, Equal("Name", "a\"b"), [new SourceColumnRef("Payload")]);
            var context = RuntimeV2TestContexts.CreateExecutionContext(
                CancellationToken.None,
                [new SchemaColumn("Payload", 0, typeof(string))],
                executionPlan: plan);
            var rows = ReadRows<string>(path, context);

            Assert.AreEqual(1, rows.Length);
            Assert.AreEqual("first", rows[0].Item0);
        });
    }

    [TestMethod]
    public void FileRowsSource_WhenAcceptedSkipAndTakeArePresent_AppliesSliceAfterPredicate()
    {
        WithCsv("Name,Age\nAlice,31\nBob,42\nCarol,53\nDan,64\n", path =>
        {
            var request = CreateRequest(
                GreaterThan("Age", 40L),
                [new SourceColumnRef("Name")],
                skip: 1,
                take: 1);
            var plan = new SeparatedValuesSchema()
                .TryPlanSource("comma", request, path, true, 0)
                .ExecutionPlan;
            var context = RuntimeV2TestContexts.CreateExecutionContext(
                CancellationToken.None,
                [new SchemaColumn("Name", 0, typeof(string))],
                executionPlan: plan);
            var rows = ReadRows<string>(path, context);

            Assert.AreEqual(1, rows.Length);
            Assert.AreEqual("Carol", rows[0].Item0);
        });
    }

    [TestMethod]
    public void FileRowsSource_WhenAcceptedTakeIsZero_DiscoversValidSourceButReadsNoDataRows()
    {
        WithCsv("Name,Age\nAlice,31\n", path =>
        {
            var request = CreateRequest(
                null,
                [new SourceColumnRef("Name")],
                skip: null,
                take: 0);
            var plan = new SeparatedValuesSchema()
                .TryPlanSource("comma", request, path, true, 0)
                .ExecutionPlan;
            var context = RuntimeV2TestContexts.CreateExecutionContext(
                CancellationToken.None,
                [new SchemaColumn("Name", 0, typeof(string))],
                executionPlan: plan);

            Assert.AreEqual(0, ReadRows<string>(path, context).Length);
        });
    }

    [TestMethod]
    public void FileRowsSource_PreservesQuotedEmptyNullShortRowsAndCsvGrammar()
    {
        WithCsv(
            "Name,Note,QuotedEmpty,NullValue,Age\r\n" +
            "Ada,\"line one\r\nline two\",\"\",,31\r\n" +
            "\r\n" +
            "Bob,\"said \"\"hello\"\"\",text\r\n",
            path =>
            {
                var required = new[]
                {
                    new SourceColumnRef("Name"),
                    new SourceColumnRef("Note"),
                    new SourceColumnRef("QuotedEmpty"),
                    new SourceColumnRef("NullValue"),
                    new SourceColumnRef("Age")
                };
                var plan = Plan(path, null, required);
                var context = RuntimeV2TestContexts.CreateExecutionContext(
                    CancellationToken.None,
                    [
                        new SchemaColumn("Name", 0, typeof(string)),
                        new SchemaColumn("Note", 1, typeof(string)),
                        new SchemaColumn("QuotedEmpty", 2, typeof(string)),
                        new SchemaColumn("NullValue", 3, typeof(string)),
                        new SchemaColumn("Age", 4, typeof(long?))
                    ],
                    executionPlan: plan);
                var rows = ReadRows<string, string, string, string, long?>(path, context);

                Assert.AreEqual(2, rows.Length);
                Assert.AreEqual(
                    new TestRow5<string, string, string, string, long?>(
                        "Ada", "line one\r\nline two", string.Empty, null!, 31L),
                    rows[0]);
                Assert.AreEqual(
                    new TestRow5<string, string, string, string, long?>(
                        "Bob", "said \"hello\"", "text", null!, null),
                    rows[1]);
            });
    }

    [TestMethod]
    public void FileRowsSource_WhenExplicitConversionFails_ThrowsInsteadOfReturningNull()
    {
        WithCsv("Age\nnot-number\n", path =>
        {
            var plan = Plan(path, null, [new SourceColumnRef("Age")]);
            var context = RuntimeV2TestContexts.CreateExecutionContext(
                CancellationToken.None,
                [new SchemaColumn("Age", 0, typeof(int))],
                executionPlan: plan);

            var exception = Assert.ThrowsExactly<FormatException>(() => ReadRows<int>(path, context));

            StringAssert.Contains(FlattenMessages(exception), "column 'Age'");
            StringAssert.Contains(FlattenMessages(exception), "Int32");
        });
    }

    private static SourceExecutionPlan Plan(
        string path,
        SourcePredicateExpression? predicate,
        IReadOnlyList<SourceColumnRef> requiredColumns)
    {
        return new SeparatedValuesSchema()
            .TryPlanSource(
                "comma",
                CreateRequest(predicate, requiredColumns, skip: null, take: null),
                path,
                true,
                0)
            .ExecutionPlan;
    }

    private static TestRow0[] ReadRows(string path, SourceExecutionContext context)
    {
        return SeparatedValuesNativeTestSource.Create(path, ",", true, 0, context)
            .Chunks
            .SelectMany(chunk => chunk)
            .ToArray();
    }

    private static TestRow1<T0>[] ReadRows<T0>(string path, SourceExecutionContext context)
    {
        return SeparatedValuesNativeTestSource.Create<T0>(path, ",", true, 0, context)
            .Chunks
            .SelectMany(chunk => chunk)
            .ToArray();
    }

    private static TestRow5<T0, T1, T2, T3, T4>[] ReadRows<T0, T1, T2, T3, T4>(
        string path,
        SourceExecutionContext context)
    {
        return SeparatedValuesNativeTestSource.Create<T0, T1, T2, T3, T4>(
                path, ",", true, 0, context)
            .Chunks
            .SelectMany(chunk => chunk)
            .ToArray();
    }

    private static SourcePlanRequest CreateRequest(
        SourcePredicateExpression? predicate,
        IReadOnlyList<SourceColumnRef>? requiredColumns,
        long? skip = 1,
        long? take = 2)
    {
        return new SourcePlanRequest
        {
            Identity = CreateIdentity(),
            RequiredColumns = requiredColumns!,
            SourceRuntimeSettings = new Dictionary<string, string>(),
            Predicate = predicate,
            OrderBy = [],
            Skip = skip,
            Take = take
        };
    }

    private static SourceIdentity CreateIdentity()
    {
        return new SourceIdentity("separatedvalues", "separatedvalues", "separatedvalues", "comma");
    }

    private static SourcePredicateComparison Equal(string columnName, object value)
    {
        return Comparison(SourcePredicateComparisonOperator.Equal, columnName, value);
    }

    private static SourcePredicateComparison GreaterThan(string columnName, object value)
    {
        return Comparison(SourcePredicateComparisonOperator.GreaterThan, columnName, value);
    }

    private static SourcePredicateComparison Comparison(
        SourcePredicateComparisonOperator comparisonOperator,
        string columnName,
        object value)
    {
        return new SourcePredicateComparison(
            comparisonOperator,
            new SourcePredicateColumn(new SourceColumnRef(columnName)),
            new SourcePredicateLiteral(value));
    }

    private static string FlattenMessages(Exception exception)
    {
        var messages = string.Empty;
        for (var current = exception; current is not null; current = current.InnerException)
            messages += current.Message + Environment.NewLine;
        return messages;
    }

    private static void WithCsv(string contents, Action<string> assertion)
    {
        var path = Path.Combine(Path.GetTempPath(), $"musoq-csv-runtime-{Guid.NewGuid():N}.csv");
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
