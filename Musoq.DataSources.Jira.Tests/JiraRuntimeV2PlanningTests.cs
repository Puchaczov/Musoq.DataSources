using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Jira.Tests;

[TestClass]
public class JiraRuntimeV2PlanningTests
{
    [TestMethod]
    public void TryPlanSource_WhenIssueStatusPredicateIsUsed_AcceptsPredicate()
    {
        var schema = new JiraSchema();
        var request = CreateRequest(Equal("Status", "Open"));

        var result = schema.TryPlanSource("issues", request, "TEST");

        Assert.IsNotNull(result.AcceptedPredicate);
        Assert.IsNull(result.ResidualPredicate);
        Assert.AreEqual(result.AcceptedPredicate, result.ExecutionPlan.AcceptedPredicate);
        AssertNoProjectionAccepted(result);
        Assert.IsTrue(result.ExecutionPlan.Properties.ContainsKey("JiraFilters"));
    }

    [TestMethod]
    public void TryPlanSource_WhenUnsupportedIssuePredicateIsUsed_KeepsPredicateResidual()
    {
        var schema = new JiraSchema();
        var request = CreateRequest(Equal("Summary", "not pushed down"));

        var result = schema.TryPlanSource("issues", request, "TEST");

        Assert.IsNull(result.AcceptedPredicate);
        Assert.IsNotNull(result.ResidualPredicate);
        Assert.AreEqual(result.AcceptedPredicate, result.ExecutionPlan.AcceptedPredicate);
        AssertNoProjectionAccepted(result);
    }

    [TestMethod]
    public void TryPlanSource_WhenSupportedAndUnsupportedIssuePredicatesAreUsed_SplitsAcceptedAndResidual()
    {
        var schema = new JiraSchema();
        var request = CreateRequest(new SourcePredicateLogical(
            SourcePredicateLogicalOperator.And,
            Equal("Status", "Open"),
            Equal("Summary", "not pushed down")));

        var result = schema.TryPlanSource("issues", request, "TEST");

        Assert.IsNotNull(result.AcceptedPredicate);
        Assert.IsNotNull(result.ResidualPredicate);
        Assert.AreEqual(result.AcceptedPredicate, result.ExecutionPlan.AcceptedPredicate);
        AssertNoProjectionAccepted(result);
    }

    [TestMethod]
    public void TryPlanSource_WhenCreatedOrderAndSliceAreRequested_AcceptsOrderAndSlice()
    {
        var schema = new JiraSchema();
        var request = CreateRequest(
            Equal("Status", "Open"),
            orderBy: [new OrderByExpression(new SourceColumnRef("CreatedAt"), OrderDirection.Descending)],
            skip: 10,
            take: 5);

        var result = schema.TryPlanSource("issues", request, "TEST");

        Assert.IsNotNull(result.AcceptedPredicate);
        Assert.IsNull(result.ResidualPredicate);
        Assert.AreEqual(1, result.AcceptedOrderBy.Count);
        Assert.AreEqual(0, result.ResidualOrderBy.Count);
        Assert.AreEqual(10, result.AcceptedSkip);
        Assert.AreEqual(5, result.AcceptedTake);
        Assert.AreEqual(result.AcceptedOrderBy.Count, result.ExecutionPlan.AcceptedOrderBy.Count);
        Assert.AreEqual(result.AcceptedSkip, result.ExecutionPlan.AcceptedSkip);
        Assert.AreEqual(result.AcceptedTake, result.ExecutionPlan.AcceptedTake);
        AssertNoProjectionAccepted(result);
    }

    [TestMethod]
    public void TryPlanSource_WhenUnsupportedOrderIsRequested_KeepsOrderAndSliceResidual()
    {
        var schema = new JiraSchema();
        var request = CreateRequest(
            Equal("Status", "Open"),
            orderBy: [new OrderByExpression(new SourceColumnRef("Key"), OrderDirection.Ascending)],
            skip: 10,
            take: 5);

        var result = schema.TryPlanSource("issues", request, "TEST");

        Assert.IsNotNull(result.AcceptedPredicate);
        Assert.AreEqual(0, result.AcceptedOrderBy.Count);
        Assert.AreEqual(1, result.ResidualOrderBy.Count);
        Assert.IsNull(result.AcceptedSkip);
        Assert.IsNull(result.AcceptedTake);
        Assert.AreEqual(10, result.ResidualSkip);
        Assert.AreEqual(5, result.ResidualTake);
        AssertNoProjectionAccepted(result);
    }

    [TestMethod]
    public void TryPlanSource_WhenProjectPredicateIsUsed_KeepsPredicateResidual()
    {
        var schema = new JiraSchema();
        var request = CreateRequest(Equal("Name", "TEST"));

        var result = schema.TryPlanSource("projects", request);

        Assert.IsNull(result.AcceptedPredicate);
        Assert.IsNotNull(result.ResidualPredicate);
        AssertNoProjectionAccepted(result);
    }

    [TestMethod]
    public void TryPlanSource_WhenRequiredColumnsArePresent_DoesNotAcceptProjection()
    {
        var schema = new JiraSchema();
        var request = CreateRequest(
            Equal("Status", "Open"),
            [new SourceColumnRef("Status")]);

        var result = schema.TryPlanSource("issues", request, "TEST");

        AssertNoProjectionAccepted(result);
    }

    private static SourcePlanRequest CreateRequest(
        SourcePredicateExpression predicate,
        IReadOnlyList<SourceColumnRef>? requiredColumns = null,
        IReadOnlyList<OrderByExpression>? orderBy = null,
        long? skip = null,
        long? take = null)
    {
        return new SourcePlanRequest
        {
            Identity = new SourceIdentity("jira", "jira", "jira", "jira"),
            RequiredColumns = requiredColumns ?? [],
            SourceRuntimeSettings = new Dictionary<string, string>(),
            Predicate = predicate,
            OrderBy = orderBy ?? [],
            Skip = skip,
            Take = take
        };
    }

    private static SourcePredicateComparison Equal(string columnName, object value)
    {
        return new SourcePredicateComparison(
            SourcePredicateComparisonOperator.Equal,
            new SourcePredicateColumn(new SourceColumnRef(columnName)),
            new SourcePredicateLiteral(value));
    }

    private static void AssertNoProjectionAccepted(SourcePlanResult result)
    {
        Assert.AreEqual(0, result.AcceptedColumns.Count);
        Assert.AreEqual(0, result.ExecutionPlan.AcceptedColumns.Count);
    }
}
