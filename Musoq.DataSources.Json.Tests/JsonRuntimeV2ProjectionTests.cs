using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Json.Tests;

[TestClass]
public class JsonRuntimeV2ProjectionTests
{
    [TestMethod]
    public void TryPlanSource_WhenScalarPredicateIsSupported_AcceptsProjectionPredicateAndSlice()
    {
        var schema = new JsonSchema();
        var predicate = Equal("Name", "Aleksander");
        var request = CreateRequest(predicate, [new SourceColumnRef("Name")]);

        var result = schema.TryPlanSource("file", request, "./JsonTestFile_First.json");

        Assert.AreEqual(1, result.AcceptedColumns.Count);
        Assert.AreEqual("Name", result.AcceptedColumns[0].Name);
        Assert.AreEqual(1, result.ExecutionPlan.AcceptedColumns.Count);
        Assert.AreEqual(predicate, result.AcceptedPredicate);
        Assert.IsNull(result.ResidualPredicate);
        Assert.AreEqual(0, result.AcceptedOrderBy.Count);
        Assert.AreEqual(request.OrderBy.Count, result.ResidualOrderBy.Count);
        Assert.AreEqual(request.Skip, result.AcceptedSkip);
        Assert.AreEqual(request.Take, result.AcceptedTake);
        Assert.IsNull(result.ResidualSkip);
        Assert.IsNull(result.ResidualTake);
        Assert.IsTrue(result.ExecutionPlan.Properties.ContainsKey(JsonPlanning.LayoutPropertyName));
    }

    [TestMethod]
    public void JsonSource_WhenProjectionIsAccepted_ProjectsOnlyAcceptedColumns()
    {
        var columns = new ISchemaColumn[]
        {
            new SchemaColumn("Age", 0, typeof(long))
        };
        var executionContext = RuntimeV2TestContexts.CreateExecutionContext(
            CancellationToken.None,
            columns,
            executionPlan: CreateExecutionPlan([new SourceColumnRef("Age")]));
        var source = new JsonSource("./JsonTestFile_First.json", executionContext);

        var rows = source.Chunks.SelectMany(chunk => chunk).ToArray();

        Assert.AreEqual(3, rows.Length);
        Assert.IsTrue(rows.All(row => row.Length == 1));
        CollectionAssert.AreEquivalent(new object[] { 24L, 11L, 45L }, rows.Select(row => row[0]).ToArray());
    }

    [TestMethod]
    public void TryPlanSource_WhenPredicateContainsOr_LeavesPredicateAndSliceResidual()
    {
        var schema = new JsonSchema();
        var predicate = new SourcePredicateLogical(
            SourcePredicateLogicalOperator.Or,
            Equal("Age", 11L),
            Equal("Age", 45L));
        var request = CreateRequest(predicate, [new SourceColumnRef("Age")]);

        var result = schema.TryPlanSource("file", request, "./JsonTestFile_First.json");

        Assert.IsNull(result.AcceptedPredicate);
        Assert.AreEqual(predicate, result.ResidualPredicate);
        Assert.IsNull(result.AcceptedSkip);
        Assert.IsNull(result.AcceptedTake);
        Assert.AreEqual(request.Skip, result.ResidualSkip);
        Assert.AreEqual(request.Take, result.ResidualTake);
    }

    private static SourcePlanRequest CreateRequest(
        SourcePredicateExpression predicate,
        IReadOnlyList<SourceColumnRef> requiredColumns)
    {
        return new SourcePlanRequest
        {
            Identity = new SourceIdentity("json", "json", "json", "file"),
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
            Identity = new SourceIdentity("json", "json", "json", "file"),
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
