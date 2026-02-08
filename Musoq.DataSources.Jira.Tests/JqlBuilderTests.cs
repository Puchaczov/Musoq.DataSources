using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Jira.Helpers;

namespace Musoq.DataSources.Jira.Tests;

[TestClass]
public class JqlBuilderTests
{
    [TestMethod]
    public void BuildJql_WithNoParameters_ShouldReturnDefaultOrder()
    {
        var parameters = new JiraFilterParameters();
        var jql = JqlBuilder.BuildJql(null, parameters);

        Assert.AreEqual("order by created DESC", jql);
    }

    [TestMethod]
    public void BuildJql_WithBaseJql_ShouldIncludeBase()
    {
        var parameters = new JiraFilterParameters();
        var jql = JqlBuilder.BuildJql("project = TEST", parameters);

        Assert.AreEqual("project = TEST", jql);
    }

    [TestMethod]
    public void BuildJql_WithStatus_ShouldBuildCorrectJql()
    {
        var parameters = new JiraFilterParameters { Status = "Open" };
        var jql = JqlBuilder.BuildJql("project = TEST", parameters);

        Assert.IsTrue(jql.Contains("project = TEST"));
        Assert.IsTrue(jql.Contains("status = \"Open\""));
    }

    [TestMethod]
    public void BuildJql_WithType_ShouldUseIssuetype()
    {
        var parameters = new JiraFilterParameters { Type = "Bug" };
        var jql = JqlBuilder.BuildJql(null, parameters);

        Assert.IsTrue(jql.Contains("issuetype = \"Bug\""));
    }

    [TestMethod]
    public void BuildJql_WithPriority_ShouldBuildCorrectJql()
    {
        var parameters = new JiraFilterParameters { Priority = "High" };
        var jql = JqlBuilder.BuildJql(null, parameters);

        Assert.IsTrue(jql.Contains("priority = \"High\""));
    }

    [TestMethod]
    public void BuildJql_WithAssignee_ShouldBuildCorrectJql()
    {
        var parameters = new JiraFilterParameters { Assignee = "john.doe" };
        var jql = JqlBuilder.BuildJql(null, parameters);

        Assert.IsTrue(jql.Contains("assignee = \"john.doe\""));
    }

    [TestMethod]
    public void BuildJql_WithUnassigned_ShouldUseEmpty()
    {
        var parameters = new JiraFilterParameters { Assignee = "unassigned" };
        var jql = JqlBuilder.BuildJql(null, parameters);

        Assert.IsTrue(jql.Contains("assignee is EMPTY"));
    }

    [TestMethod]
    public void BuildJql_WithReporter_ShouldBuildCorrectJql()
    {
        var parameters = new JiraFilterParameters { Reporter = "jane.doe" };
        var jql = JqlBuilder.BuildJql(null, parameters);

        Assert.IsTrue(jql.Contains("reporter = \"jane.doe\""));
    }

    [TestMethod]
    public void BuildJql_WithProjectKey_ShouldBuildCorrectJql()
    {
        var parameters = new JiraFilterParameters { ProjectKey = "MYPROJ" };
        var jql = JqlBuilder.BuildJql(null, parameters);

        Assert.IsTrue(jql.Contains("project = MYPROJ"));
    }

    [TestMethod]
    public void BuildJql_WithIssueKey_ShouldBuildCorrectJql()
    {
        var parameters = new JiraFilterParameters { Key = "TEST-123" };
        var jql = JqlBuilder.BuildJql(null, parameters);

        Assert.IsTrue(jql.Contains("key = TEST-123"));
    }

    [TestMethod]
    public void BuildJql_WithLabels_ShouldBuildCorrectJql()
    {
        var parameters = new JiraFilterParameters();
        parameters.Labels.Add("bug");
        parameters.Labels.Add("urgent");
        var jql = JqlBuilder.BuildJql(null, parameters);

        Assert.IsTrue(jql.Contains("labels = \"bug\""));
        Assert.IsTrue(jql.Contains("labels = \"urgent\""));
    }

    [TestMethod]
    public void BuildJql_WithComponents_ShouldBuildCorrectJql()
    {
        var parameters = new JiraFilterParameters();
        parameters.Components.Add("Backend");
        var jql = JqlBuilder.BuildJql(null, parameters);

        Assert.IsTrue(jql.Contains("component = \"Backend\""));
    }

    [TestMethod]
    public void BuildJql_WithCreatedAfter_ShouldBuildCorrectJql()
    {
        var date = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var parameters = new JiraFilterParameters { CreatedAfter = date };
        var jql = JqlBuilder.BuildJql(null, parameters);

        Assert.IsTrue(jql.Contains("created >= \"2024-01-15 10:30\""));
    }

    [TestMethod]
    public void BuildJql_WithCreatedBefore_ShouldBuildCorrectJql()
    {
        var date = new DateTimeOffset(2024, 6, 30, 23, 59, 0, TimeSpan.Zero);
        var parameters = new JiraFilterParameters { CreatedBefore = date };
        var jql = JqlBuilder.BuildJql(null, parameters);

        Assert.IsTrue(jql.Contains("created <= \"2024-06-30 23:59\""));
    }

    [TestMethod]
    public void BuildJql_WithUpdatedRange_ShouldBuildCorrectJql()
    {
        var start = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2024, 12, 31, 23, 59, 0, TimeSpan.Zero);
        var parameters = new JiraFilterParameters 
        { 
            UpdatedAfter = start,
            UpdatedBefore = end
        };
        var jql = JqlBuilder.BuildJql(null, parameters);

        Assert.IsTrue(jql.Contains("updated >= \"2024-01-01 00:00\""));
        Assert.IsTrue(jql.Contains("updated <= \"2024-12-31 23:59\""));
    }

    [TestMethod]
    public void BuildJql_WithSummaryContains_ShouldUseTildeOperator()
    {
        var parameters = new JiraFilterParameters { SummaryContains = "login bug" };
        var jql = JqlBuilder.BuildJql(null, parameters);

        Assert.IsTrue(jql.Contains("summary ~ \"login bug\""));
    }

    [TestMethod]
    public void BuildJql_WithTextSearch_ShouldUseTildeOperator()
    {
        var parameters = new JiraFilterParameters { TextSearch = "error 404" };
        var jql = JqlBuilder.BuildJql(null, parameters);

        Assert.IsTrue(jql.Contains("text ~ \"error 404\""));
    }

    [TestMethod]
    public void BuildJql_WithMultipleParameters_ShouldCombineWithAnd()
    {
        var parameters = new JiraFilterParameters 
        { 
            Status = "Open",
            Type = "Bug",
            Priority = "Critical",
            Assignee = "developer"
        };
        var jql = JqlBuilder.BuildJql("project = TEST", parameters);

        Assert.IsTrue(jql.Contains(" AND "));
        Assert.IsTrue(jql.Contains("project = TEST"));
        Assert.IsTrue(jql.Contains("status = \"Open\""));
        Assert.IsTrue(jql.Contains("issuetype = \"Bug\""));
        Assert.IsTrue(jql.Contains("priority = \"Critical\""));
        Assert.IsTrue(jql.Contains("assignee = \"developer\""));
    }

    [TestMethod]
    public void BuildJql_WithSpecialCharacters_ShouldEscapeCorrectly()
    {
        var parameters = new JiraFilterParameters { SummaryContains = "test \"quoted\" value" };
        var jql = JqlBuilder.BuildJql(null, parameters);

        Assert.IsTrue(jql.Contains("summary ~ \"test \\\"quoted\\\" value\""));
    }

    [TestMethod]
    public void BuildJql_WithParentKey_ShouldBuildCorrectJql()
    {
        var parameters = new JiraFilterParameters { ParentKey = "PARENT-1" };
        var jql = JqlBuilder.BuildJql(null, parameters);

        Assert.IsTrue(jql.Contains("parent = PARENT-1"));
    }

    [TestMethod]
    public void BuildJql_WithFixVersion_ShouldBuildCorrectJql()
    {
        var parameters = new JiraFilterParameters { FixVersion = "1.0.0" };
        var jql = JqlBuilder.BuildJql(null, parameters);

        Assert.IsTrue(jql.Contains("fixVersion = \"1.0.0\""));
    }

    [TestMethod]
    public void BuildJql_WithResolution_ShouldBuildCorrectJql()
    {
        var parameters = new JiraFilterParameters { Resolution = "Done" };
        var jql = JqlBuilder.BuildJql(null, parameters);

        Assert.IsTrue(jql.Contains("resolution = \"Done\""));
    }
}
