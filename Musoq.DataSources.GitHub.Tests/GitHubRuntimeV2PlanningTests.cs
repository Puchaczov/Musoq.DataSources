using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.GitHub.Tests;

[TestClass]
public class GitHubRuntimeV2PlanningTests
{
    [TestMethod]
    public void TryPlanSource_WhenIssueStatePredicateIsUsed_AcceptsPredicate()
    {
        var schema = new GitHubSchema();
        var request = CreateRequest(Equal("State", "open"));

        var result = schema.TryPlanSource("issues", request, "owner", "repo");

        Assert.IsNotNull(result.AcceptedPredicate);
        Assert.IsNull(result.ResidualPredicate);
        Assert.AreEqual(result.AcceptedPredicate, result.ExecutionPlan.AcceptedPredicate);
        AssertNoProjectionAccepted(result);
        Assert.IsTrue(result.ExecutionPlan.Properties.ContainsKey("GitHubFilters"));
    }

    [TestMethod]
    public void TryPlanSource_WhenReleasePredicateIsUsed_KeepsPredicateResidual()
    {
        var schema = new GitHubSchema();
        var request = CreateRequest(Equal("Draft", false));

        var result = schema.TryPlanSource("releases", request, "owner", "repo");

        Assert.IsNull(result.AcceptedPredicate);
        Assert.IsNotNull(result.ResidualPredicate);
        Assert.AreEqual(result.AcceptedPredicate, result.ExecutionPlan.AcceptedPredicate);
        AssertNoProjectionAccepted(result);
    }

    [TestMethod]
    public void TryPlanSource_WhenRepositoryLanguagePredicateIsUsed_AcceptsPredicate()
    {
        var schema = new GitHubSchema();
        var request = CreateRequest(Equal("r.Language", "C#"));

        var result = schema.TryPlanSource("repositories", request);

        Assert.IsNotNull(result.AcceptedPredicate);
        Assert.IsNull(result.ResidualPredicate);
        Assert.AreEqual(result.AcceptedPredicate, result.ExecutionPlan.AcceptedPredicate);
        AssertNoProjectionAccepted(result);
    }

    [TestMethod]
    public void TryPlanSource_WhenSkipAndTakeHaveNoResidualWork_AcceptsSlice()
    {
        var schema = new GitHubSchema();
        var request = CreateRequest(null, skip: 10, take: 5);

        var result = schema.TryPlanSource("issues", request, "owner", "repo");

        Assert.AreEqual(10, result.AcceptedSkip);
        Assert.AreEqual(5, result.AcceptedTake);
        Assert.AreEqual(result.AcceptedSkip, result.ExecutionPlan.AcceptedSkip);
        Assert.AreEqual(result.AcceptedTake, result.ExecutionPlan.AcceptedTake);
        Assert.IsNull(result.ResidualSkip);
        Assert.IsNull(result.ResidualTake);
        AssertNoProjectionAccepted(result);
    }

    [TestMethod]
    public void TryPlanSource_WhenSkipAndTakeHaveResidualPredicate_KeepsSliceResidual()
    {
        var schema = new GitHubSchema();
        var request = CreateRequest(Equal("Title", "not pushed down"), skip: 10, take: 5);

        var result = schema.TryPlanSource("issues", request, "owner", "repo");

        Assert.IsNull(result.AcceptedSkip);
        Assert.IsNull(result.AcceptedTake);
        Assert.IsNull(result.ExecutionPlan.AcceptedSkip);
        Assert.IsNull(result.ExecutionPlan.AcceptedTake);
        Assert.AreEqual(10, result.ResidualSkip);
        Assert.AreEqual(5, result.ResidualTake);
        AssertNoProjectionAccepted(result);
    }

    [TestMethod]
    public void TryPlanSource_WhenOrderIsRequested_KeepsOrderAndSliceResidual()
    {
        var schema = new GitHubSchema();
        var request = CreateRequest(
            Equal("State", "open"),
            skip: 10,
            take: 5,
            orderBy: [new OrderByExpression(new SourceColumnRef("CreatedAt"), OrderDirection.Descending)]);

        var result = schema.TryPlanSource("issues", request, "owner", "repo");

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
    public void TryPlanSource_WhenRequiredColumnsArePresent_DoesNotAcceptProjection()
    {
        var schema = new GitHubSchema();
        var request = CreateRequest(
            Equal("State", "open"),
            requiredColumns: [new SourceColumnRef("State")]);

        var result = schema.TryPlanSource("issues", request, "owner", "repo");

        AssertNoProjectionAccepted(result);
    }

    private static SourcePlanRequest CreateRequest(
        SourcePredicateExpression? predicate,
        int? skip = null,
        int? take = null,
        IReadOnlyList<SourceColumnRef>? requiredColumns = null,
        IReadOnlyList<OrderByExpression>? orderBy = null)
    {
        return new SourcePlanRequest
        {
            Identity = new SourceIdentity("github", "github", "github", "github"),
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
