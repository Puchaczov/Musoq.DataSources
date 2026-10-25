using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.DataSources.GitHub.Entities;
using Musoq.DataSources.GitHub.Tests.TestHelpers;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;
using Musoq.Schema.Diagnostics;
using Musoq.Schema.Optimization;
using Octokit;
using Musoq.DataSources.Common;
using Musoq.Evaluator;
using System.Diagnostics;

namespace Musoq.DataSources.GitHub.Tests;

[TestClass]
public class EarlyPatternTests
{
    [TestMethod]
    public void MeasurePatternSelectivity()
    {
        var values = Enumerable.Range(0, 1000).Select(i => Raw(MockEntityFactory.CreateIssue(title: i.ToString("D4")))).ToArray();
        foreach (var (pattern, expected) in new[] { ("^000", 10), ("^0[0-4]", 500), ("^0", 1000) })
        foreach (var enabled in new[] { false, true })
        {
            var predicate = new SourcePredicateRegexMatch(new("Title"), pattern, TimeSpan.FromMilliseconds(31));
            int Read()
            {
                var rows = PatternPage.Map(values, enabled ? new NativePatternExecution([predicate]) : null,
                    GitHubNativePatterns.Issue, static value => new IssueEntity(value), default);
                return enabled ? rows.Count : rows.Count(row => ProviderRegex.Match(predicate, row.Title) == true);
            }
            Assert.AreEqual(expected, Read());
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            var timer = Stopwatch.StartNew();
            for (var i = 0; i < 20; i++) Assert.AreEqual(expected, Read());
            timer.Stop();
            Console.WriteLine($"native={enabled}; admitted={expected}/1000; iterations=20; elapsedMs={timer.Elapsed.TotalMilliseconds}; allocatedBytes={GC.GetAllocatedBytesForCurrentThread() - allocated}");
        }
    }

    [TestMethod]
    public void NativeTimeout_PreservesFiniteTypedCauseBeforeWrapping()
    {
        var api = CreateApi(_ => [Raw(MockEntityFactory.CreateIssue(title: new string('a', 50000) + "!"))]);
        var timeout = TimeSpan.FromMilliseconds(1);
        var plan = new GitHubSchema(api).TryPlanSource("issues", new() { Identity = new("github", "s", "s", "issues"),
            Predicate = new SourcePredicateRegexMatch(new("Title"), "^(?=(a+)+$)a+$", timeout) });
        var metrics = new PatternWorkMetrics();
        var context = RuntimeV2TestContexts.CreateExecutionContext(executionPlan: plan.ExecutionPlan, sourceDiagnostics: new SourceDiagnostics(metrics));
        var failure = Assert.ThrowsException<System.Text.RegularExpressions.RegexMatchTimeoutException>(() =>
            new Sources.Issues.IssuesSource(api, context, "owner", "repo").Chunks.ToArray());
        Assert.AreEqual(timeout, failure.MatchTimeout);
        Assert.AreEqual(0L, metrics["Pattern.ConstructedRows"]);
    }

    [TestMethod]
    public void SharedDefaultClient_OverlappingExecutionsOwnTheirPredicates()
    {
        var api = CreateApi(_ => [Raw(MockEntityFactory.CreateIssue(title: "Alpha")), Raw(MockEntityFactory.CreateIssue(title: "Beta"))]);
        var schema = new GitHubSchema(api);
        Parallel.For(0, 8, index =>
        {
            var expected = index % 2 == 0 ? "Alpha" : "Beta";
            var plan = schema.TryPlanSource("issues", new() { Identity = new("github", "s", "s", "issues"),
                Predicate = new SourcePredicateRegexMatch(new("Title"), "^" + expected + "$", TimeSpan.FromMilliseconds(31)), Take = 1 });
            var context = RuntimeV2TestContexts.CreateExecutionContext(executionPlan: plan.ExecutionPlan);
            for (var repeat = 0; repeat < 2; repeat++)
                Assert.AreEqual(expected, new Sources.Issues.IssuesSource(api, context, "owner", "repo").Chunks.SelectMany(chunk => chunk).Single().Title);
        });
    }

    [TestMethod]
    public void CancellationAfterSdkResponse_PreventsWrapperConstruction()
    {
        using var cancellation = new CancellationTokenSource();
        var api = CreateApi(_ => { cancellation.Cancel(); return [Raw(MockEntityFactory.CreateIssue(title: "Alpha"))]; });
        var plan = new GitHubSchema(api).TryPlanSource("issues", new() { Identity = new("github", "s", "s", "issues"),
            Predicate = new SourcePredicateRegexMatch(new("Title"), ".", TimeSpan.FromMilliseconds(31)) });
        var metrics = new PatternWorkMetrics();
        var context = RuntimeV2TestContexts.CreateExecutionContext(cancellationToken: cancellation.Token,
            executionPlan: plan.ExecutionPlan, sourceDiagnostics: new SourceDiagnostics(metrics));
        Assert.AreEqual(0, new Sources.Issues.IssuesSource(api, context, "owner", "repo").Chunks.Sum(chunk => chunk.Count));
        Assert.AreEqual(0L, metrics["Pattern.ConstructedRows"]);
    }

    [TestMethod]
    public void AllRawSdkBindings_ExecuteBothFamiliesWithExistingGetterSemantics()
    {
        T RawValue<T>(object row, string field) => (T)row.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(row)!;
        var repository = MockEntityFactory.CreateRepository();
        Verify(RawValue<Repository>(repository, "_repository"), GitHubNativePatterns.Repository, value => new RepositoryEntity(value), GitHubPatternColumns.RepositoryEntity);
        var issue = MockEntityFactory.CreateIssue();
        Verify(RawValue<Issue>(issue, "_issue"), GitHubNativePatterns.Issue, value => new IssueEntity(value), GitHubPatternColumns.IssueEntity);
        var pr = MockEntityFactory.CreatePullRequest();
        Verify(RawValue<PullRequest>(pr, "_pullRequest"), GitHubNativePatterns.PullRequest, value => new PullRequestEntity(value), GitHubPatternColumns.PullRequestEntity);
        var commit = MockEntityFactory.CreateCommit();
        Verify(RawValue<GitHubCommit>(commit, "_commit"), GitHubNativePatterns.Commit, value => new CommitEntity(value), GitHubPatternColumns.CommitEntity);
        var branch = MockEntityFactory.CreateBranch();
        Verify(new GitHubNativePatterns.BranchInput(RawValue<Branch>(branch, "_branch"), branch.RepositoryOwner, branch.RepositoryName), GitHubNativePatterns.Branch,
            value => new BranchEntity(value.Value, value.Owner, value.Repository), GitHubPatternColumns.BranchEntity);
        var release = MockEntityFactory.CreateRelease();
        Verify(RawValue<Release>(release, "_release"), GitHubNativePatterns.Release, value => new ReleaseEntity(value), GitHubPatternColumns.ReleaseEntity);
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
            var native = new NativePatternExecution([predicate]);
            var constructed = 0;
            var page = PatternPage.Map([raw], native, bindings, value => { constructed++; return create(value); }, default);
            var value = rows.Read(row, name);
            var expected = (kind is null ? Operators.RLikeDynamic(value, "(?i)a") : Operators.LikePrepared(value, Operators.PrepareLike(pattern))) != negated;
            Assert.AreEqual(expected ? 1 : 0, page.Count, name);
            Assert.AreEqual(page.Count, constructed, name);
            Assert.AreEqual(1, PatternPage.RawCount(page));
        }
    }

    [TestMethod]
    public void DefaultSdkAdapter_ContinuesEmptyFilteredPage_AndStopsAfterAcceptedWindow()
    {
        foreach (var direct in new[] { false, true })
        {
            var requests = new List<int>();
            var api = CreateApi(parameters =>
            {
                var page = int.Parse(parameters["page"]);
                requests.Add(page);
                Assert.IsTrue(page <= 2, "An accepted window must prevent a third page request.");
                return Enumerable.Range(0, 100).Select(i => Raw(MockEntityFactory.CreateIssue(id: page * 100 + i, title: page == 2 && i < 2 ? "keep" : "reject"))).ToList();
            });
            var schema = new GitHubSchema(api);
            var predicate = new SourcePredicateRegexMatch(new("Title"), "^keep$", TimeSpan.FromMilliseconds(31));
            var plan = schema.TryPlanSource("issues", new() { Identity = new("github", "s", "s", "issues"), Predicate = predicate, Skip = 1, Take = 1 });
            var metrics = new PatternWorkMetrics();
            var context = RuntimeV2TestContexts.CreateExecutionContext(executionPlan: plan.ExecutionPlan, sourceDiagnostics: new SourceDiagnostics(metrics));
            var source = direct ? new Sources.Issues.IssuesSource(api, context, "owner", "repo")
                : schema.GetRowSource<IssueEntity, SourceArguments<string, SourceArguments<string, EmptySourceArguments>>>("issues", context, SourceArguments.Create("owner", SourceArguments.Create("repo", SourceArguments.Empty)));
            Assert.AreEqual(201L, source.Chunks.SelectMany(chunk => chunk).Single().Id);
            CollectionAssert.AreEqual(new[] { 1, 2 }, requests);
            Assert.AreEqual(200L, metrics["Pattern.Candidates"]);
            Assert.AreEqual(198L, metrics["Pattern.RejectedCandidates"]);
            Assert.AreEqual(2L, metrics["Pattern.ConstructedRows"]);
            Assert.AreEqual(2L, metrics["Pattern.ApiPages"]);
            Assert.AreSame(predicate, plan.ExecutionPlan.PredicateApplications.Single().Predicate);
        }
    }

    private static GitHubApi CreateApi(Func<IDictionary<string, string>, List<Issue>> fetch)
    {
        var connection = new Mock<IConnection>();
        connection.SetupGet(value => value.BaseAddress).Returns(new Uri("https://api.github.com/"));
        Task<IApiResponse<List<Issue>>> Reply(IDictionary<string, string> parameters)
        {
            var http = new Mock<IResponse>();
            http.SetupGet(value => value.StatusCode).Returns(HttpStatusCode.OK);
            http.SetupGet(value => value.Headers).Returns(new Dictionary<string, string>());
            http.SetupGet(value => value.ApiInfo).Returns(new ApiInfo(new Dictionary<string, Uri>(), [], [], "", null!, TimeSpan.Zero));
            var response = new Mock<IApiResponse<List<Issue>>>();
            response.SetupGet(value => value.Body).Returns(fetch(parameters));
            response.SetupGet(value => value.HttpResponse).Returns(http.Object);
            return Task.FromResult(response.Object);
        }
        connection.Setup(value => value.Get<List<Issue>>(It.IsAny<Uri>(), It.IsAny<IDictionary<string, string>>())).Returns((Uri uri, IDictionary<string, string> parameters) => Reply(parameters));
        connection.Setup(value => value.Get<List<Issue>>(It.IsAny<Uri>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<string>())).Returns((Uri uri, IDictionary<string, string> parameters, string _) => Reply(parameters));
        connection.Setup(value => value.Get<List<Issue>>(It.IsAny<Uri>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns((Uri uri, IDictionary<string, string> parameters, string _, CancellationToken token) => Reply(parameters));
        connection.Setup(value => value.Get<List<Issue>>(It.IsAny<Uri>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<Func<object, object>>())).Returns((Uri uri, IDictionary<string, string> parameters, string _, CancellationToken token, Func<object, object> preprocess) => Reply(parameters));
        return new GitHubApi(new GitHubClient(connection.Object));
    }

    private static Issue Raw(IssueEntity row) => (Issue)typeof(IssueEntity).GetField("_issue", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(row)!;
}

