using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Atlassian.Jira;
using Atlassian.Jira.Remote;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.DataSources.Jira.Entities;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;
using Musoq.Schema.Diagnostics;
using Musoq.Schema.Optimization;
using Musoq.DataSources.Common;
using Musoq.Evaluator;

namespace Musoq.DataSources.Jira.Tests;

[TestClass]
public class EarlyPatternTests
{
    [TestMethod]
    public void AllRawSdkBindings_ExecuteBothFamiliesBeforeWrapping()
    {
        var sdk = Atlassian.Jira.Jira.CreateRestClient("https://example.invalid/", "user", "token");
        var issue = new Issue(sdk, new RemoteIssue { id = "1", key = "PROJ-1", summary = "Alpha", description = "Body", labels = ["alpha"] }, "user");
        Verify(issue, JiraNativePatterns.Issue, value => (IJiraIssue)new IssueEntity(value), JiraPatternColumns.IJiraIssue);
        var project = new Project(sdk, new RemoteProject { id = "1", key = "PROJ", name = "Alpha" });
        Verify(project, JiraNativePatterns.Project, value => (IJiraProject)new ProjectEntity(value), JiraPatternColumns.IJiraProject);
        var comment = new Comment(new RemoteComment { id = "1", author = "author", body = "Alpha" });
        Verify(new JiraNativePatterns.CommentInput(comment, "PROJ-1"), JiraNativePatterns.Comment,
            value => (IJiraComment)new CommentEntity(value.Value, value.IssueKey), JiraPatternColumns.IJiraComment);
    }

    private static void Verify<TValue, TRow>(TValue raw, PatternColumnBindings<TValue> bindings, Func<TValue, TRow> create, PatternColumnBindings<TRow> rows)
    {
        CollectionAssert.AreEquivalent(rows.Names.ToArray(), bindings.Names.ToArray());
        var row = create(raw);
        foreach (var name in bindings.Names)
        foreach (var kind in Enum.GetValues<SourceStringMatchKind>().Select(value => (SourceStringMatchKind?)value).Append(null))
        foreach (var negated in new[] { false, true })
        {
            var pattern = kind switch { SourceStringMatchKind.Prefix => "a%", SourceStringMatchKind.Suffix => "%a", SourceStringMatchKind.Contains => "%a%", _ => "a" };
            SourcePredicatePatternMatch predicate = kind is null ? new SourcePredicateRegexMatch(new(name), "(?i)a", TimeSpan.FromMilliseconds(31), isNegated: negated)
                : new SourcePredicateStringMatch(new(name), kind.Value, pattern, "a", isNegated: negated);
            var constructed = 0;
            var page = PatternPage.Map([raw], new NativePatternExecution([predicate]), bindings, value => { constructed++; return create(value); }, default);
            var value = rows.Read(row, name);
            var expected = (kind is null ? Operators.RLikeDynamic(value, "(?i)a") : Operators.LikePrepared(value, Operators.PrepareLike(pattern))) != negated;
            Assert.AreEqual(expected ? 1 : 0, page.Count, name);
            Assert.AreEqual(page.Count, constructed, name);
            Assert.AreEqual(1, PatternPage.RawCount(page));
        }
    }

    [TestMethod]
    public void DefaultSdkAdapter_RetainsRawPageOffsetsAndStopsAfterAcceptedWindow()
    {
        foreach (var direct in new[] { false, true })
        {
            var service = new Mock<IIssueService>(MockBehavior.Strict);
            var services = new ServiceLocator();
            services.Register(() => service.Object);
            var sdk = new Atlassian.Jira.Jira(services, new JiraCache());
            var offsets = new List<int>();
            service.Setup(value => value.GetIssuesFromJqlAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns((string jql, int? size, int offset, CancellationToken token) =>
                {
                    offsets.Add(offset);
                    Assert.AreEqual(50, size);
                    Assert.AreEqual("project = TEST ORDER BY created DESC", jql);
                    Assert.IsTrue(offset <= 50, "Accepted output window must prevent a third request.");
                    var rows = Enumerable.Range(0, 50).Select(i => new Issue(sdk, new RemoteIssue
                    { key = "TEST-" + (offset + i), id = (offset + i).ToString(), summary = offset == 50 && i < 2 ? "keep" : "reject" }, "user")).ToList();
                    var page = new Mock<IPagedQueryResult<Issue>>();
                    page.Setup(value => value.GetEnumerator()).Returns(() => rows.GetEnumerator());
                    page.SetupGet(value => value.StartAt).Returns(offset);
                    page.SetupGet(value => value.ItemsPerPage).Returns(50);
                    page.SetupGet(value => value.TotalItems).Returns(100);
                    return Task.FromResult(page.Object);
                });
            var api = new JiraApi(sdk);
            var schema = new JiraSchema(api);
            var predicate = new SourcePredicateRegexMatch(new("Summary"), "^keep$", TimeSpan.FromMilliseconds(31));
            var plan = schema.TryPlanSource("issues", new() { Identity = new("jira", "s", "s", "issues"), Predicate = predicate, Skip = 1, Take = 1 });
            var metrics = new PatternWorkMetrics();
            var context = RuntimeV2TestContexts.CreateExecutionContext(executionPlan: plan.ExecutionPlan, sourceDiagnostics: new SourceDiagnostics(metrics));
            var source = direct ? new Sources.Issues.IssuesSource(api, context, "TEST")
                : schema.GetRowSource<IJiraIssue, SourceArguments<string, EmptySourceArguments>>("issues", context, SourceArguments.Create("TEST", SourceArguments.Empty));
            Assert.AreEqual("TEST-51", source.Chunks.SelectMany(chunk => chunk).Single().Key);
            CollectionAssert.AreEqual(new[] { 0, 50 }, offsets);
            Assert.AreEqual(100L, metrics["Pattern.Candidates"]);
            Assert.AreEqual(98L, metrics["Pattern.RejectedCandidates"]);
            Assert.AreEqual(2L, metrics["Pattern.ConstructedRows"]);
            Assert.AreEqual(2L, metrics["Pattern.ApiPages"]);
        }
    }
}
