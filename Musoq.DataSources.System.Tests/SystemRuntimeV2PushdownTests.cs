#nullable enable

using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.System.Tests;

[TestClass]
public class SystemRuntimeV2PushdownTests
{
    [TestMethod]
    public void TryPlanSource_WhenRangePredicateNaturalOrderAndSliceAreRequested_AcceptsThem()
    {
        var predicate = And(GreaterOrEqual("r.Value", 2L), LessThan("Value", 8L));
        var orderBy = new[]
        {
            new OrderByExpression(new SourceColumnRef("Value"), OrderDirection.Ascending)
        };
        var request = CreateRequest("range", predicate, orderBy, skip: 1, take: 2);

        var result = new SystemSchema().TryPlanSource("range", request, 0, 10);

        Assert.IsNotNull(result.AcceptedPredicate);
        Assert.IsNull(result.ResidualPredicate);
        Assert.AreEqual(1, result.AcceptedOrderBy.Count);
        Assert.AreEqual(0, result.ResidualOrderBy.Count);
        Assert.AreEqual(1, result.AcceptedSkip);
        Assert.AreEqual(2, result.AcceptedTake);
        AssertPlanMatchesExecution(result);
    }

    [TestMethod]
    public void TryPlanSource_WhenDualIsRequested_RejectsAll()
    {
        var request = CreateRequest("dual", Equal("Value", 1L), [], skip: null, take: null);

        var result = new SystemSchema().TryPlanSource("dual", request);

        Assert.IsNull(result.AcceptedPredicate);
        Assert.AreSame(request.Predicate, result.ResidualPredicate);
        Assert.AreEqual(0, result.AcceptedColumns.Count);
    }

    [TestMethod]
    public void RangeSource_WhenExecutionPlanHasPredicateAndSlice_EmitsFilteredRows()
    {
        var predicate = And(GreaterOrEqual("Value", 3L), LessOrEqual("Value", 7L));
        var executionPlan = new SourceExecutionPlan
        {
            Identity = CreateIdentity("range"),
            AcceptedPredicate = predicate,
            AcceptedOrderBy = [new OrderByExpression(new SourceColumnRef("Value"), OrderDirection.Ascending)],
            AcceptedSkip = 1,
            AcceptedTake = 2
        };
        var source = new RangeSource(0, 10, RuntimeV2TestContexts.CreateExecutionContext(executionPlan: executionPlan));

        var values = source.Chunks.SelectMany(chunk => chunk).Select(row => row.Value).ToArray();

        CollectionAssert.AreEqual(new[] { 4L, 5L }, values);
    }

    private static SourcePlanRequest CreateRequest(
        string sourceName,
        SourcePredicateExpression? predicate,
        IReadOnlyList<OrderByExpression> orderBy,
        long? skip,
        long? take)
    {
        return new SourcePlanRequest
        {
            Identity = CreateIdentity(sourceName),
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

    private static SourceIdentity CreateIdentity(string sourceName)
    {
        return new SourceIdentity("system", "system", "system", sourceName);
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

    private static SourcePredicateComparison LessThan(string columnName, object value)
    {
        return Compare(SourcePredicateComparisonOperator.LessThan, columnName, value);
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
