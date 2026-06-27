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
    }

    [TestMethod]
    public void TryPlanSource_WhenRepositoryLanguagePredicateIsUsed_AcceptsPredicate()
    {
        var schema = new GitHubSchema();
        var request = CreateRequest(Equal("r.Language", "C#"));

        var result = schema.TryPlanSource("repositories", request);

        Assert.IsNotNull(result.AcceptedPredicate);
        Assert.IsNull(result.ResidualPredicate);
    }

    [TestMethod]
    public void TryPlanSource_WhenSkipAndTakeHaveNoResidualWork_AcceptsSlice()
    {
        var schema = new GitHubSchema();
        var request = CreateRequest(null, skip: 10, take: 5);

        var result = schema.TryPlanSource("issues", request, "owner", "repo");

        Assert.AreEqual(10, result.AcceptedSkip);
        Assert.AreEqual(5, result.AcceptedTake);
        Assert.IsNull(result.ResidualSkip);
        Assert.IsNull(result.ResidualTake);
    }

    [TestMethod]
    public void TryPlanSource_WhenSkipAndTakeHaveResidualPredicate_KeepsSliceResidual()
    {
        var schema = new GitHubSchema();
        var request = CreateRequest(Equal("Title", "not pushed down"), skip: 10, take: 5);

        var result = schema.TryPlanSource("issues", request, "owner", "repo");

        Assert.IsNull(result.AcceptedSkip);
        Assert.IsNull(result.AcceptedTake);
        Assert.AreEqual(10, result.ResidualSkip);
        Assert.AreEqual(5, result.ResidualTake);
    }

    private static SourcePlanRequest CreateRequest(
        SourcePredicateExpression? predicate,
        int? skip = null,
        int? take = null)
    {
        return new SourcePlanRequest
        {
            Identity = new SourceIdentity("github", "github", "github", "github"),
            RequiredColumns = [],
            SourceRuntimeSettings = new Dictionary<string, string>(),
            Predicate = predicate,
            OrderBy = [],
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
}
