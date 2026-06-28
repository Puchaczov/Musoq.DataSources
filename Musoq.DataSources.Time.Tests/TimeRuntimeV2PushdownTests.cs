#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Time.Tests;

[TestClass]
public class TimeRuntimeV2PushdownTests
{
    [TestMethod]
    public void TryPlanSource_WhenDateTimePredicateNaturalOrderAndSliceAreRequested_AcceptsThem()
    {
        var start = DateTimeOffset.Parse("2000-01-01T00:00:00Z");
        var predicate = And(
            GreaterOrEqual("t.DateTime", start.AddMinutes(1)),
            LessOrEqual("DateTime", start.AddMinutes(4)));
        var orderBy = new[]
        {
            new OrderByExpression(new SourceColumnRef("DateTime"), OrderDirection.Ascending)
        };
        var request = CreateRequest("interval", predicate, orderBy, skip: 1, take: 2);

        var result = new TimeSchema().TryPlanSource("interval", request, start, start.AddMinutes(5), "minutes");

        Assert.IsNotNull(result.AcceptedPredicate);
        Assert.IsNull(result.ResidualPredicate);
        Assert.AreEqual(1, result.AcceptedOrderBy.Count);
        Assert.AreEqual(0, result.ResidualOrderBy.Count);
        Assert.AreEqual(1, result.AcceptedSkip);
        Assert.IsNull(result.ResidualSkip);
        Assert.AreEqual(2, result.AcceptedTake);
        Assert.IsNull(result.ResidualTake);
        AssertPlanMatchesExecution(result);
    }

    [TestMethod]
    public void TryPlanSource_WhenUnsupportedPredicateOrOrderIsRequested_LeavesSliceResidual()
    {
        var request = CreateRequest(
            "interval",
            Equal("Day", 1),
            [new OrderByExpression(new SourceColumnRef("DateTime"), OrderDirection.Descending)],
            skip: 1,
            take: 2);

        var result = new TimeSchema().TryPlanSource(
            "interval",
            request,
            DateTimeOffset.Parse("2000-01-01T00:00:00Z"),
            DateTimeOffset.Parse("2000-01-01T00:05:00Z"),
            "minutes");

        Assert.IsNull(result.AcceptedPredicate);
        Assert.AreSame(request.Predicate, result.ResidualPredicate);
        Assert.AreEqual(0, result.AcceptedOrderBy.Count);
        Assert.AreEqual(1, result.ResidualOrderBy.Count);
        Assert.IsNull(result.AcceptedSkip);
        Assert.AreEqual(1, result.ResidualSkip);
        Assert.IsNull(result.AcceptedTake);
        Assert.AreEqual(2, result.ResidualTake);
        AssertPlanMatchesExecution(result);
    }

    [TestMethod]
    public void TimeSource_WhenExecutionPlanHasPredicateAndSlice_EmitsFilteredRows()
    {
        var start = DateTimeOffset.Parse("2000-01-01T00:00:00Z");
        var predicate = And(
            GreaterOrEqual("DateTime", start.AddMinutes(1)),
            LessOrEqual("DateTime", start.AddMinutes(4)));
        var executionPlan = new SourceExecutionPlan
        {
            Identity = CreateIdentity("interval"),
            AcceptedPredicate = predicate,
            AcceptedOrderBy = [new OrderByExpression(new SourceColumnRef("DateTime"), OrderDirection.Ascending)],
            AcceptedSkip = 1,
            AcceptedTake = 2
        };
        var source = new TimeSource(
            start,
            start.AddMinutes(5),
            "minutes",
            RuntimeV2TestContexts.CreateExecutionContext(CancellationToken.None, executionPlan: executionPlan));

        var rows = source.Chunks.SelectMany(chunk => chunk).ToArray();

        Assert.AreEqual(2, rows.Length);
        Assert.AreEqual(start.AddMinutes(2), rows[0].DateTime);
        Assert.AreEqual(start.AddMinutes(3), rows[1].DateTime);
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
        return new SourceIdentity("time", "time", "time", sourceName);
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
