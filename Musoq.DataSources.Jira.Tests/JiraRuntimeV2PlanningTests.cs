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
    }

    [TestMethod]
    public void TryPlanSource_WhenProjectPredicateIsUsed_KeepsPredicateResidual()
    {
        var schema = new JiraSchema();
        var request = CreateRequest(Equal("Name", "TEST"));

        var result = schema.TryPlanSource("projects", request);

        Assert.IsNull(result.AcceptedPredicate);
        Assert.IsNotNull(result.ResidualPredicate);
    }

    private static SourcePlanRequest CreateRequest(SourcePredicateExpression predicate)
    {
        return new SourcePlanRequest
        {
            Identity = new SourceIdentity("jira", "jira", "jira", "jira"),
            RequiredColumns = [],
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
}
