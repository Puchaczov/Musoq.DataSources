#nullable enable

using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.FlatFile;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema.Optimization;

namespace Musoq.Schema.FlatFile.Tests;

[TestClass]
public class FlatFileRuntimeV2PushdownTests
{
    [TestMethod]
    public void TryPlanSource_WhenLineNumberPredicateNaturalOrderAndSliceAreRequested_AcceptsThem()
    {
        var predicate = And(GreaterOrEqual("f.LineNumber", 2), LessOrEqual("LineNumber", 5));
        var orderBy = new[]
        {
            new OrderByExpression(new SourceColumnRef("LineNumber"), OrderDirection.Ascending)
        };
        var request = CreateRequest(predicate, orderBy, skip: 1, take: 2);

        var result = new FlatFileSchema().TryPlanSource("file", request, "./TestMultilineFile.txt");

        Assert.IsNotNull(result.AcceptedPredicate);
        Assert.IsNull(result.ResidualPredicate);
        Assert.AreEqual(1, result.AcceptedOrderBy.Count);
        Assert.AreEqual(0, result.ResidualOrderBy.Count);
        Assert.AreEqual(1, result.AcceptedSkip);
        Assert.AreEqual(2, result.AcceptedTake);
        AssertPlanMatchesExecution(result);
    }

    [TestMethod]
    public void TryPlanSource_WhenLinePredicateIsRequested_LeavesPredicateAndSliceResidual()
    {
        var request = CreateRequest(Equal("Line", "line3"), [], skip: 1, take: 2);

        var result = new FlatFileSchema().TryPlanSource("file", request, "./TestMultilineFile.txt");

        Assert.IsNull(result.AcceptedPredicate);
        Assert.AreSame(request.Predicate, result.ResidualPredicate);
        Assert.IsNull(result.AcceptedSkip);
        Assert.AreEqual(1, result.ResidualSkip);
        Assert.IsNull(result.AcceptedTake);
        Assert.AreEqual(2, result.ResidualTake);
        AssertPlanMatchesExecution(result);
    }

    [TestMethod]
    public void FlatFileSource_WhenExecutionPlanHasPredicateAndSlice_EmitsFilteredRows()
    {
        var predicate = And(GreaterOrEqual("LineNumber", 2), LessOrEqual("LineNumber", 5));
        var executionPlan = new SourceExecutionPlan
        {
            Identity = CreateIdentity(),
            AcceptedPredicate = predicate,
            AcceptedOrderBy = [new OrderByExpression(new SourceColumnRef("LineNumber"), OrderDirection.Ascending)],
            AcceptedSkip = 1,
            AcceptedTake = 2
        };
        var source = new FlatFileSource(
            "./TestMultilineFile.txt",
            RuntimeV2TestContexts.CreateExecutionContext(executionPlan: executionPlan));

        var rows = source.Chunks.SelectMany(chunk => chunk).ToArray();

        CollectionAssert.AreEqual(new[] { 3, 4 }, rows.Select(row => row.LineNumber).ToArray());
        CollectionAssert.AreEqual(new[] { "line3", "line" }, rows.Select(row => row.Line).ToArray());
    }

    private static SourcePlanRequest CreateRequest(
        SourcePredicateExpression? predicate,
        IReadOnlyList<OrderByExpression> orderBy,
        long? skip,
        long? take)
    {
        return new SourcePlanRequest
        {
            Identity = CreateIdentity(),
            RequiredColumns = [],
            SourceRuntimeSettings = new Dictionary<string, string>(),
            Predicate = predicate,
            OrderBy = orderBy,
            Skip = skip,
            Take = take
        };
    }

    private static void AssertPlanMatchesExecution(SourcePlanResult result)
    {
        Assert.AreSame(result.AcceptedPredicate, result.ExecutionPlan.AcceptedPredicate);
        Assert.AreEqual(result.AcceptedOrderBy.Count, result.ExecutionPlan.AcceptedOrderBy.Count);
        Assert.AreEqual(result.AcceptedSkip, result.ExecutionPlan.AcceptedSkip);
        Assert.AreEqual(result.AcceptedTake, result.ExecutionPlan.AcceptedTake);
        Assert.AreEqual(0, result.AcceptedColumns.Count);
        Assert.AreEqual(0, result.ExecutionPlan.AcceptedColumns.Count);
    }

    private static SourceIdentity CreateIdentity()
    {
        return new SourceIdentity("flatfile", "flatfile", "flatfile", "file");
    }

    private static SourcePredicateLogical And(SourcePredicateExpression left, SourcePredicateExpression right)
    {
        return new SourcePredicateLogical(SourcePredicateLogicalOperator.And, left, right);
    }

    private static SourcePredicateComparison Equal(string columnName, object value)
    {
        return Compare(SourcePredicateComparisonOperator.Equal, columnName, value);
    }

    private static SourcePredicateComparison GreaterOrEqual(string columnName, object value)
    {
        return Compare(SourcePredicateComparisonOperator.GreaterOrEqual, columnName, value);
    }

    private static SourcePredicateComparison LessOrEqual(string columnName, object value)
    {
        return Compare(SourcePredicateComparisonOperator.LessOrEqual, columnName, value);
    }

    private static SourcePredicateComparison Compare(
        SourcePredicateComparisonOperator op,
        string columnName,
        object value)
    {
        return new SourcePredicateComparison(
            op,
            new SourcePredicateColumn(new SourceColumnRef(columnName)),
            new SourcePredicateLiteral(value));
    }
}
