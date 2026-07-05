using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues.Tests;

[TestClass]
public class SeparatedValuesRuntimeV2ProjectionTests
{
    [TestMethod]
    public void TryPlanSource_WhenRequiredColumnsAndSupportedPredicateArePresent_AcceptsProjectionPredicateAndSlice()
    {
        var schema = new SeparatedValuesSchema();
        var predicate = Equal("Name", "Alice");
        var request = CreateRequest(predicate, [new SourceColumnRef("Name")]);

        var result = schema.TryPlanSource("comma", request, "data.csv", true, 0);

        Assert.AreEqual(1, result.AcceptedColumns.Count);
        Assert.AreEqual("Name", result.AcceptedColumns[0].Name);
        Assert.AreEqual(1, result.ExecutionPlan.AcceptedColumns.Count);
        Assert.AreEqual(predicate, result.AcceptedPredicate);
        Assert.IsNull(result.ResidualPredicate);
        Assert.AreEqual(request.Skip, result.AcceptedSkip);
        Assert.IsNull(result.ResidualSkip);
        Assert.AreEqual(request.Take, result.AcceptedTake);
        Assert.IsNull(result.ResidualTake);
    }

    [TestMethod]
    public void TryPlanSource_WhenNoColumnsAreRequired_AcceptsZeroColumnProjection()
    {
        var schema = new SeparatedValuesSchema();
        var request = CreateRequest(null, []);

        var result = schema.TryPlanSource("comma", request, "data.csv", true, 0);
        var readPlan = SeparatedValuesReadPlan.From(result.ExecutionPlan);

        Assert.AreEqual(0, result.AcceptedColumns.Count);
        Assert.AreEqual(0, result.ExecutionPlan.AcceptedColumns.Count);
        Assert.IsTrue(readPlan.ProjectionAccepted);
    }

    [TestMethod]
    public void TryPlanSource_WhenRequiredColumnsAreNull_DoesNotAcceptProjection()
    {
        var schema = new SeparatedValuesSchema();
        var request = CreateRequest(null, null);

        var result = schema.TryPlanSource("comma", request, "data.csv", true, 0);
        var readPlan = SeparatedValuesReadPlan.From(result.ExecutionPlan);

        Assert.AreEqual(0, result.AcceptedColumns.Count);
        Assert.IsFalse(readPlan.ProjectionAccepted);
    }

    [TestMethod]
    public void TryPlanSource_WhenOrPredicateIsPresent_KeepsPredicateResidual()
    {
        var schema = new SeparatedValuesSchema();
        var predicate = new SourcePredicateLogical(
            SourcePredicateLogicalOperator.Or,
            Equal("Name", "Alice"),
            Equal("Name", "Bob"));
        var request = CreateRequest(predicate, [new SourceColumnRef("Name")]);

        var result = schema.TryPlanSource("comma", request, "data.csv", true, 0);

        Assert.IsNull(result.AcceptedPredicate);
        Assert.AreEqual(predicate, result.ResidualPredicate);
        Assert.IsNull(result.AcceptedSkip);
        Assert.AreEqual(request.Skip, result.ResidualSkip);
    }

    [TestMethod]
    public void StreamRowsSource_WhenProjectionIsAccepted_SkipsUnrequestedColumnConversion()
    {
        var columns = CreateColumnsWithUnsupportedPayload();
        var executionContext = RuntimeV2TestContexts.CreateExecutionContext(
            CancellationToken.None,
            columns,
            executionPlan: CreateExecutionPlan([new SourceColumnRef("Name")]));
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Name,Payload\r\nAlice,unsupported\r\n"));
        var source = new SeparatedValuesFromStreamRowsSource(stream, ",", true, 0, executionContext);

        var rows = source.Chunks.SelectMany(chunk => chunk).ToArray();

        Assert.AreEqual(1, rows.Length);
        Assert.AreEqual(1, rows[0].Length);
        Assert.AreEqual("Alice", rows[0][0]);
    }

    [TestMethod]
    public void FileRowsSource_WhenProjectionIsAccepted_SkipsUnrequestedColumnConversion()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        File.WriteAllText(tempFile, "Name,Payload\r\nAlice,unsupported\r\n", Encoding.UTF8);

        try
        {
            var columns = CreateColumnsWithUnsupportedPayload();
            var executionContext = RuntimeV2TestContexts.CreateExecutionContext(
                CancellationToken.None,
                columns,
                executionPlan: CreateExecutionPlan([new SourceColumnRef("Name")]));
            var source = new SeparatedValuesFromFileRowsSource(tempFile, ",", true, 0, executionContext);

            var rows = source.Chunks.SelectMany(chunk => chunk).ToArray();

            Assert.AreEqual(1, rows.Length);
            Assert.AreEqual(1, rows[0].Length);
            Assert.AreEqual("Alice", rows[0][0]);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [TestMethod]
    public void FileRowsSource_WhenExecutionPlanHasNoProperties_StillUsesProjectionFallback()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        File.WriteAllText(tempFile, "Name,Payload\r\nAlice,unsupported\r\n", Encoding.UTF8);

        try
        {
            var columns = CreateColumnsWithUnsupportedPayload();
            var executionContext = RuntimeV2TestContexts.CreateExecutionContext(
                CancellationToken.None,
                columns,
                executionPlan: CreateExecutionPlan([new SourceColumnRef("Name")]));
            var source = new SeparatedValuesFromFileRowsSource(tempFile, ",", true, 0, executionContext);

            var rows = source.Chunks.SelectMany(chunk => chunk).ToArray();

            Assert.AreEqual(1, rows.Length);
            Assert.AreEqual(1, rows[0].Length);
            Assert.AreEqual("Alice", rows[0][0]);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }


    [TestMethod]
    public void FileRowsSource_WhenZeroColumnProjectionIsAccepted_EmitsEmptyRows()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        File.WriteAllText(tempFile, "Name,Age\r\nAlice,31\r\nBob,42\r\n", Encoding.UTF8);

        try
        {
            var columns = CreateNameAgeColumns();
            var plan = new SeparatedValuesSchema()
                .TryPlanSource("comma", CreateRequest(null, [], skip: null, take: null), tempFile, true, 0)
                .ExecutionPlan;
            var executionContext = RuntimeV2TestContexts.CreateExecutionContext(
                CancellationToken.None,
                columns,
                executionPlan: plan);
            var source = new SeparatedValuesFromFileRowsSource(tempFile, ",", true, 0, executionContext);

            var rows = source.Chunks.SelectMany(chunk => chunk).ToArray();

            Assert.AreEqual(2, rows.Length);
            Assert.IsTrue(rows.All(row => row.Length == 0));
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [TestMethod]
    public void FileRowsSource_WhenAcceptedPredicateUsesUnprojectedColumn_FiltersBeforeProjection()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        File.WriteAllText(tempFile, "Name,Age\r\nAlice,31\r\nBob,42\r\n", Encoding.UTF8);

        try
        {
            var columns = CreateNameAgeColumns();
            var request = CreateRequest(
                GreaterThan("Age", 40),
                [new SourceColumnRef("Name")],
                skip: null,
                take: null);
            var plan = new SeparatedValuesSchema()
                .TryPlanSource("comma", request, tempFile, true, 0)
                .ExecutionPlan;
            var executionContext = RuntimeV2TestContexts.CreateExecutionContext(
                CancellationToken.None,
                columns,
                executionPlan: plan);
            var source = new SeparatedValuesFromFileRowsSource(tempFile, ",", true, 0, executionContext);

            var rows = source.Chunks.SelectMany(chunk => chunk).ToArray();

            Assert.AreEqual(1, rows.Length);
            Assert.AreEqual("Bob", rows[0][0]);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [TestMethod]
    public void FileRowsSource_WhenAcceptedSkipAndTakeArePresent_AppliesSliceInSource()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        File.WriteAllText(tempFile, "Name,Age\r\nAlice,31\r\nBob,42\r\nCarol,53\r\n", Encoding.UTF8);

        try
        {
            var columns = CreateNameAgeColumns();
            var request = CreateRequest(
                null,
                [new SourceColumnRef("Name")],
                skip: 1,
                take: 1);
            var plan = new SeparatedValuesSchema()
                .TryPlanSource("comma", request, tempFile, true, 0)
                .ExecutionPlan;
            var executionContext = RuntimeV2TestContexts.CreateExecutionContext(
                CancellationToken.None,
                columns,
                executionPlan: plan);
            var source = new SeparatedValuesFromFileRowsSource(tempFile, ",", true, 0, executionContext);

            var rows = source.Chunks.SelectMany(chunk => chunk).ToArray();

            Assert.AreEqual(1, rows.Length);
            Assert.AreEqual("Bob", rows[0][0]);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [TestMethod]
    public void FileRowsSource_WhenAcceptedTakeIsZero_ReturnsNoRows()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        File.WriteAllText(tempFile, "Name,Age\r\nAlice,31\r\nBob,42\r\n", Encoding.UTF8);

        try
        {
            var columns = CreateNameAgeColumns();
            var request = CreateRequest(
                null,
                [new SourceColumnRef("Name")],
                skip: null,
                take: 0);
            var plan = new SeparatedValuesSchema()
                .TryPlanSource("comma", request, tempFile, true, 0)
                .ExecutionPlan;
            var executionContext = RuntimeV2TestContexts.CreateExecutionContext(
                CancellationToken.None,
                columns,
                executionPlan: plan);
            var source = new SeparatedValuesFromFileRowsSource(tempFile, ",", true, 0, executionContext);

            var rows = source.Chunks.SelectMany(chunk => chunk).ToArray();

            Assert.AreEqual(0, rows.Length);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [TestMethod]
    public void FileRowsSource_WhenAcceptedPredicateColumnIsMissing_Throws()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        File.WriteAllText(tempFile, "Name,Age\r\nAlice,31\r\n", Encoding.UTF8);

        try
        {
            var columns = CreateNameAgeColumns();
            var request = CreateRequest(
                Equal("Missing", "value"),
                [new SourceColumnRef("Name")],
                skip: null,
                take: null);
            var plan = new SeparatedValuesSchema()
                .TryPlanSource("comma", request, tempFile, true, 0)
                .ExecutionPlan;
            var executionContext = RuntimeV2TestContexts.CreateExecutionContext(
                CancellationToken.None,
                columns,
                executionPlan: plan);
            var source = new SeparatedValuesFromFileRowsSource(tempFile, ",", true, 0, executionContext);

            var exception = Assert.ThrowsException<InvalidOperationException>(() =>
                source.Chunks.SelectMany(chunk => chunk).ToArray());

            StringAssert.Contains(exception.Message, "Missing");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [TestMethod]
    public void FileRowsSource_WhenAcceptedPredicateValueCannotBeParsed_TreatsRowAsNotMatched()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        File.WriteAllText(tempFile, "Name,Age\r\nAlice,not-number\r\nBob,\r\nCarol,42\r\n", Encoding.UTF8);

        try
        {
            var columns = CreateNameAgeColumns();
            var request = CreateRequest(
                GreaterThan("Age", 40),
                [new SourceColumnRef("Name")],
                skip: null,
                take: null);
            var plan = new SeparatedValuesSchema()
                .TryPlanSource("comma", request, tempFile, true, 0)
                .ExecutionPlan;
            var executionContext = RuntimeV2TestContexts.CreateExecutionContext(
                CancellationToken.None,
                columns,
                executionPlan: plan);
            var source = new SeparatedValuesFromFileRowsSource(tempFile, ",", true, 0, executionContext);

            var rows = source.Chunks.SelectMany(chunk => chunk).ToArray();

            Assert.AreEqual(1, rows.Length);
            Assert.AreEqual("Carol", rows[0][0]);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }


    private static ISchemaColumn[] CreateColumnsWithUnsupportedPayload()
    {
        return
        [
            new SchemaColumn("Name", 0, typeof(string)),
            new SchemaColumn("Payload", 1, typeof(object))
        ];
    }

    private static ISchemaColumn[] CreateNameAgeColumns()
    {
        return
        [
            new SchemaColumn("Name", 0, typeof(string)),
            new SchemaColumn("Age", 1, typeof(int))
        ];
    }

    private static SourcePlanRequest CreateRequest(
        SourcePredicateExpression predicate,
        IReadOnlyList<SourceColumnRef> requiredColumns,
        long? skip = 1,
        long? take = 2)
    {
        return new SourcePlanRequest
        {
            Identity = new SourceIdentity("separatedvalues", "separatedvalues", "separatedvalues", "comma"),
            RequiredColumns = requiredColumns,
            SourceRuntimeSettings = new Dictionary<string, string>(),
            Predicate = predicate,
            OrderBy = [],
            Skip = skip,
            Take = take
        };
    }

    private static SourceExecutionPlan CreateExecutionPlan(IReadOnlyList<SourceColumnRef> acceptedColumns)
    {
        return new SourceExecutionPlan
        {
            Identity = new SourceIdentity("separatedvalues", "separatedvalues", "separatedvalues", "comma"),
            AcceptedColumns = acceptedColumns,
            AcceptedOrderBy = []
        };
    }

    private static SourcePredicateComparison Equal(string columnName, object value)
    {
        return Comparison(
            SourcePredicateComparisonOperator.Equal,
            columnName,
            value);
    }

    private static SourcePredicateComparison GreaterThan(string columnName, object value)
    {
        return Comparison(
            SourcePredicateComparisonOperator.GreaterThan,
            columnName,
            value);
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
}
