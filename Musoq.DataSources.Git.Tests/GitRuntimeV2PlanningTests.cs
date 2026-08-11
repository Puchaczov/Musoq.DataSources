using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Git.Entities;
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
        AssertZeroColumnProjectionAccepted(result);
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
        AssertZeroColumnProjectionAccepted(result);
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
        AssertZeroColumnProjectionAccepted(result);
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
        AssertZeroColumnProjectionAccepted(result);
    }

    [TestMethod]
    public void TryPlanSource_WhenRequiredColumnsArePresent_AcceptsKnownProjection()
    {
        var schema = new GitSchema();
        var request = CreateRequest(
            Equal("Author", "anonymous"),
            [new SourceColumnRef("Author")]);

        var result = schema.TryPlanSource("commits", request, "repo");

        CollectionAssert.AreEqual(new[] { new SourceColumnRef("Author") }, result.AcceptedColumns.ToArray());
        CollectionAssert.AreEqual(new[] { new SourceColumnRef("Author") }, result.ExecutionPlan.AcceptedColumns.ToArray());
        Assert.IsTrue(result.ExecutionPlan.Properties.ContainsKey("GitProjection"));
    }

    [TestMethod]
    public void TryPlanSource_WhenOrContainsUnsupportedPredicate_LeavesWholeOrResidual()
    {
        var supported = Equal("Author", "anonymous");
        var unsupported = Equal("Message", "initial commit");
        var request = CreateRequest(new SourcePredicateLogical(SourcePredicateLogicalOperator.Or, supported, unsupported));

        var result = new GitSchema().TryPlanSource("commits", request, "repo");

        Assert.IsNull(result.AcceptedPredicate);
        Assert.AreEqual(request.Predicate, result.ResidualPredicate);
    }

    [TestMethod]
    public void TryPlanSource_WhenInAndNullChecksUseKnownColumns_AcceptsThem()
    {
        SourcePredicateExpression[] predicates =
        [
            new SourcePredicateIn(
                new SourcePredicateColumn(new SourceColumnRef("Author")),
                [new SourcePredicateLiteral("anonymous")]),
            new SourcePredicateNullCheck(new SourcePredicateColumn(new SourceColumnRef("Author")), IsNegated: true)
        ];

        foreach (var predicate in predicates)
        {
            var result = new GitSchema().TryPlanSource("commits", CreateRequest(predicate), "repo");
            Assert.AreEqual(predicate, result.AcceptedPredicate);
            Assert.IsNull(result.ResidualPredicate);
            var filters = (GitFilterParameters)result.ExecutionPlan.Properties[GitSourcePlanner.FiltersPropertyName]!;
            Assert.AreEqual(predicate, filters.RawPredicate);
        }
    }

    [TestMethod]
    public void TryPlanSource_WhenNaturalWindowHasNoResidualWork_AcceptsIt()
    {
        var request = CreateRequest(Equal("Author", "anonymous")) with
        {
            Skip = 2,
            Take = 3
        };

        var result = new GitSchema().TryPlanSource("commits", request, "repo");

        Assert.AreEqual(0, result.AcceptedOrderBy.Count);
        Assert.AreEqual(0, result.ResidualOrderBy.Count);
        Assert.AreEqual(2L, result.AcceptedSkip);
        Assert.IsNull(result.ResidualSkip);
        Assert.AreEqual(3L, result.AcceptedTake);
        Assert.IsNull(result.ResidualTake);
        Assert.AreEqual(result.AcceptedSkip, result.ExecutionPlan.AcceptedSkip);
        Assert.AreEqual(result.AcceptedTake, result.ExecutionPlan.AcceptedTake);
    }

    [TestMethod]
    public void TryPlanSource_WhenResidualPredicateOrOrderExists_KeepsWindowResidual()
    {
        var request = CreateRequest(Equal("Message", "initial commit")) with
        {
            OrderBy = [new OrderByExpression(new SourceColumnRef("Author"), OrderDirection.Ascending)],
            Skip = 2,
            Take = 3
        };

        var result = new GitSchema().TryPlanSource("commits", request, "repo");

        Assert.IsNull(result.AcceptedSkip);
        Assert.AreEqual(2L, result.ResidualSkip);
        Assert.IsNull(result.AcceptedTake);
        Assert.AreEqual(3L, result.ResidualTake);
        Assert.IsTrue(result.Diagnostics.Any(diagnostic => diagnostic.Optimization == "GitSlicePushdown"));
    }

    [TestMethod]
    public void TryPlanSource_WhenProjectionIsEmpty_UsesCardinalityOnlySnapshotButKeepsPredicateDependency()
    {
        var result = new GitSchema().TryPlanSource("commits", CreateRequest(Equal("Author", "anonymous")), "repo");
        var projection = (GitProjection)result.ExecutionPlan.Properties[GitSourcePlanner.ProjectionPropertyName]!;

        Assert.IsTrue(projection.IsAccepted);
        Assert.IsTrue(projection.Includes(nameof(CommitEntity.Author)));
        Assert.IsFalse(projection.Includes(nameof(CommitEntity.Message)));
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

    private static void AssertZeroColumnProjectionAccepted(SourcePlanResult result)
    {
        Assert.AreEqual(0, result.AcceptedColumns.Count);
        Assert.AreEqual(0, result.ExecutionPlan.AcceptedColumns.Count);
        Assert.IsTrue(result.ExecutionPlan.Properties.ContainsKey("GitProjection"));
    }
}
