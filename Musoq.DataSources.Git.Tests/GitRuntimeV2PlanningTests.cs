using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Git.Tests;

[TestClass]
public class GitRuntimeV2PlanningTests
{
    [TestMethod]
    public void TryPlanSource_WhenCommitAuthorPredicateIsUsed_AcceptsPredicate()
    {
        var schema = new GitSchema();
        var request = CreateRequest(Equal("Author", "anonymous"));

        var result = schema.TryPlanSource("commits", request, "repo");

        Assert.IsNotNull(result.AcceptedPredicate);
        Assert.IsNull(result.ResidualPredicate);
        Assert.AreEqual(result.AcceptedPredicate, result.ExecutionPlan.AcceptedPredicate);
        AssertNoProjectionAccepted(result);
        Assert.IsTrue(result.ExecutionPlan.Properties.ContainsKey("GitFilters"));
    }

    [TestMethod]
    public void TryPlanSource_WhenCommitPredicateHasUnsupportedSide_KeepsResidualPredicate()
    {
        var schema = new GitSchema();
        var request = CreateRequest(new SourcePredicateLogical(
            SourcePredicateLogicalOperator.And,
            Equal("Author", "anonymous"),
            Equal("Message", "initial commit")));

        var result = schema.TryPlanSource("commits", request, "repo");

        Assert.IsNotNull(result.AcceptedPredicate);
        Assert.IsNotNull(result.ResidualPredicate);
        Assert.AreEqual(result.AcceptedPredicate, result.ExecutionPlan.AcceptedPredicate);
        AssertNoProjectionAccepted(result);
    }

    [TestMethod]
    public void TryPlanSource_WhenBranchPredicateIsUsed_AcceptsPredicate()
    {
        var schema = new GitSchema();
        var request = CreateRequest(Equal("b.IsRemote", false));

        var result = schema.TryPlanSource("branches", request, "repo");

        Assert.IsNotNull(result.AcceptedPredicate);
        Assert.IsNull(result.ResidualPredicate);
        Assert.AreEqual(result.AcceptedPredicate, result.ExecutionPlan.AcceptedPredicate);
        AssertNoProjectionAccepted(result);
    }

    [TestMethod]
    public void TryPlanSource_WhenCommitDatePredicateIsUsed_AcceptsPredicate()
    {
        var schema = new GitSchema();
        var request = CreateRequest(new SourcePredicateComparison(
            SourcePredicateComparisonOperator.GreaterOrEqual,
            new SourcePredicateColumn(new SourceColumnRef("CommittedWhen")),
            new SourcePredicateLiteral(new DateTimeOffset(2024, 11, 8, 0, 0, 0, TimeSpan.Zero))));

        var result = schema.TryPlanSource("commits", request, "repo");

        Assert.IsNotNull(result.AcceptedPredicate);
        Assert.IsNull(result.ResidualPredicate);
        Assert.AreEqual(result.AcceptedPredicate, result.ExecutionPlan.AcceptedPredicate);
        AssertNoProjectionAccepted(result);
    }

    [TestMethod]
    public void TryPlanSource_WhenRequiredColumnsArePresent_DoesNotAcceptProjection()
    {
        var schema = new GitSchema();
        var request = CreateRequest(
            Equal("Author", "anonymous"),
            [new SourceColumnRef("Author")]);

        var result = schema.TryPlanSource("commits", request, "repo");

        AssertNoProjectionAccepted(result);
    }

    private static SourcePlanRequest CreateRequest(
        SourcePredicateExpression predicate,
        IReadOnlyList<SourceColumnRef>? requiredColumns = null)
    {
        return new SourcePlanRequest
        {
            Identity = new SourceIdentity("git", "git", "git", "git"),
            RequiredColumns = requiredColumns ?? [],
            SourceRuntimeSettings = new Dictionary<string, string>(),
            Predicate = predicate,
            OrderBy = []
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
