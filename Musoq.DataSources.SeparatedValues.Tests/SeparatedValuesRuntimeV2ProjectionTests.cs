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
    public void TryPlanSource_WhenRequiredColumnsArePresent_AcceptsProjectionOnly()
    {
        var schema = new SeparatedValuesSchema();
        var predicate = Equal("Name", "Alice");
        var request = CreateRequest(predicate, [new SourceColumnRef("Name")]);

        var result = schema.TryPlanSource("comma", request, "data.csv", true, 0);

        Assert.AreEqual(1, result.AcceptedColumns.Count);
        Assert.AreEqual("Name", result.AcceptedColumns[0].Name);
        Assert.AreEqual(1, result.ExecutionPlan.AcceptedColumns.Count);
        Assert.IsNull(result.AcceptedPredicate);
        Assert.AreEqual(predicate, result.ResidualPredicate);
        Assert.AreEqual(request.Skip, result.ResidualSkip);
        Assert.AreEqual(request.Take, result.ResidualTake);
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

    private static ISchemaColumn[] CreateColumnsWithUnsupportedPayload()
    {
        return
        [
            new SchemaColumn("Name", 0, typeof(string)),
            new SchemaColumn("Payload", 1, typeof(object))
        ];
    }

    private static SourcePlanRequest CreateRequest(
        SourcePredicateExpression predicate,
        IReadOnlyList<SourceColumnRef> requiredColumns)
    {
        return new SourcePlanRequest
        {
            Identity = new SourceIdentity("separatedvalues", "separatedvalues", "separatedvalues", "comma"),
            RequiredColumns = requiredColumns,
            SourceRuntimeSettings = new Dictionary<string, string>(),
            Predicate = predicate,
            OrderBy = [],
            Skip = 1,
            Take = 2
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
        return new SourcePredicateComparison(
            SourcePredicateComparisonOperator.Equal,
            new SourcePredicateColumn(new SourceColumnRef(columnName)),
            new SourcePredicateLiteral(value));
    }
}
