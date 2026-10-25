using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.DataSources.Jira.Entities;
using Musoq.DataSources.Jira.Tests.TestHelpers;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;

namespace Musoq.DataSources.Jira.Tests;

[TestClass]
public sealed class NativePatternTests
{
    [TestMethod]
    public void ProjectAndCommentStringColumns_MatchCore()
    {
        var api = new Mock<IJiraApi>(MockBehavior.Strict);
        api.Setup(a => a.GetProjectsAsync()).ReturnsAsync(new[] { MockEntityFactory.CreateProject(name: "Alpha"), MockEntityFactory.CreateProject(id: "2", key: "B", name: "Beta") });
        api.Setup(a => a.GetCommentsAsync("A-1")).ReturnsAsync(new[] { MockEntityFactory.CreateComment(body: "Alpha"), MockEntityFactory.CreateComment(id: "2", body: "Beta") });
        api.Setup(a => a.GetIssuesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(new[] { MockEntityFactory.CreateIssue(summary: "Alpha"), MockEntityFactory.CreateIssue(key: "B-2", summary: "Beta") });
        var schema = new JiraSchema(api.Object);
        var columns = PatternContractVerifier.Verify<IJiraIssue, SourceArguments<string, EmptySourceArguments>>(schema, "issues", SourceArguments.Create("A", SourceArguments.Empty), ["A"]);
        columns += PatternContractVerifier.Verify<IJiraProject, EmptySourceArguments>(schema, "projects", SourceArguments.Empty, []);
        columns += PatternContractVerifier.Verify<IJiraComment, SourceArguments<string, EmptySourceArguments>>(schema, "comments", SourceArguments.Create("A-1", SourceArguments.Empty), ["A-1"]);
        PatternContractVerifier.VerifySource(schema, "issues", [], context => new Sources.Issues.IssuesSource(api.Object, context, "A"));
        PatternContractVerifier.VerifySource(schema, "projects", [], context => new Sources.Projects.ProjectsSource(api.Object, context));
        PatternContractVerifier.VerifySource(schema, "comments", [], context => new Sources.Comments.CommentsSource(api.Object, context, "A-1"));

        Assert.AreEqual(6 + 2 * PatternContractVerifier.CasesPerColumn * columns, api.Invocations.Count);
    }
}
