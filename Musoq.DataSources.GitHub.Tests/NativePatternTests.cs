using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.DataSources.GitHub.Entities;
using Musoq.DataSources.GitHub.Tests.TestHelpers;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;
using Octokit;

namespace Musoq.DataSources.GitHub.Tests;

[TestClass]
public sealed class NativePatternTests
{
    [TestMethod]
    public void AllApiStringColumns_MatchCoreWithoutExtraRequests()
    {
        var api = new Mock<IGitHubApi>(MockBehavior.Strict);
        api.Setup(a => a.GetUserRepositoriesAsync(It.IsAny<RepositoryRequest>(), It.IsAny<int?>(), It.IsAny<int?>())).ReturnsAsync(new[] { MockEntityFactory.CreateRepository(name: "alpha", description: "beta"), MockEntityFactory.CreateRepository(id: 2, name: "Beta") });
        api.Setup(a => a.GetIssuesAsync("owner", "repo", It.IsAny<RepositoryIssueRequest>(), It.IsAny<int?>(), It.IsAny<int?>())).ReturnsAsync(new[] { MockEntityFactory.CreateIssue(1, 1, "Alpha"), MockEntityFactory.CreateIssue(2, 2, "Beta") });
        api.Setup(a => a.GetPullRequestsAsync("owner", "repo", It.IsAny<PullRequestRequest>(), It.IsAny<int?>(), It.IsAny<int?>())).ReturnsAsync(new[] { MockEntityFactory.CreatePullRequest(), MockEntityFactory.CreatePullRequest(id: 2) });
        api.Setup(a => a.GetCommitsAsync("owner", "repo", It.IsAny<CommitRequest>(), It.IsAny<int?>(), It.IsAny<int?>())).ReturnsAsync(new[] { MockEntityFactory.CreateCommit(), MockEntityFactory.CreateCommit(sha: "beta") });
        api.Setup(a => a.GetBranchesAsync("owner", "repo", It.IsAny<int?>(), It.IsAny<int?>())).ReturnsAsync(new[] { MockEntityFactory.CreateBranch(), MockEntityFactory.CreateBranch(name: "beta") });
        api.Setup(a => a.GetReleasesAsync("owner", "repo", It.IsAny<int?>(), It.IsAny<int?>())).ReturnsAsync(new[] { MockEntityFactory.CreateRelease(), MockEntityFactory.CreateRelease(id: 2) });
        api.Setup(a => a.GetBranchSpecificCommitsAsync("owner", "repo", "main", "feature")).ReturnsAsync(new[] { MockEntityFactory.CreateCommit(), MockEntityFactory.CreateCommit(sha: "beta") });
        var schema = new GitHubSchema(api.Object);
        var columns = PatternContractVerifier.Verify<RepositoryEntity, EmptySourceArguments>(schema, "repositories", SourceArguments.Empty, []);
        var args = SourceArguments.Create("owner", SourceArguments.Create("repo", SourceArguments.Empty));
        columns += PatternContractVerifier.Verify<IssueEntity, SourceArguments<string, SourceArguments<string, EmptySourceArguments>>>(schema, "issues", args, ["owner", "repo"]);
        columns += PatternContractVerifier.Verify<PullRequestEntity, SourceArguments<string, SourceArguments<string, EmptySourceArguments>>>(schema, "pullrequests", args, ["owner", "repo"]);
        columns += PatternContractVerifier.Verify<CommitEntity, SourceArguments<string, SourceArguments<string, EmptySourceArguments>>>(schema, "commits", args, ["owner", "repo"]);
        columns += PatternContractVerifier.Verify<BranchEntity, SourceArguments<string, SourceArguments<string, EmptySourceArguments>>>(schema, "branches", args, ["owner", "repo"]);
        var branchArgs = SourceArguments.Create("owner", SourceArguments.Create("repo", SourceArguments.Create("main", SourceArguments.Create("feature", SourceArguments.Empty))));
        columns += PatternContractVerifier.Verify<CommitEntity, SourceArguments<string, SourceArguments<string, SourceArguments<string, SourceArguments<string, EmptySourceArguments>>>>>(schema, "branchcommits", branchArgs, ["owner", "repo", "main", "feature"]);
        columns += PatternContractVerifier.Verify<ReleaseEntity, SourceArguments<string, SourceArguments<string, EmptySourceArguments>>>(schema, "releases", args, ["owner", "repo"]);
        PatternContractVerifier.VerifySource(schema, "repositories", [], context => new Sources.Repositories.RepositoriesSource(api.Object, context));
        PatternContractVerifier.VerifySource(schema, "issues", [], context => new Sources.Issues.IssuesSource(api.Object, context, "owner", "repo"));
        PatternContractVerifier.VerifySource(schema, "pullrequests", [], context => new Sources.PullRequests.PullRequestsSource(api.Object, context, "owner", "repo"));
        PatternContractVerifier.VerifySource(schema, "commits", [], context => new Sources.Commits.CommitsSource(api.Object, context, "owner", "repo"));
        PatternContractVerifier.VerifySource(schema, "branches", [], context => new Sources.Branches.BranchesSource(api.Object, context, "owner", "repo"));
        PatternContractVerifier.VerifySource(schema, "releases", [], context => new Sources.Releases.ReleasesSource(api.Object, context, "owner", "repo"));
        PatternContractVerifier.VerifySource(schema, "branchcommits", [], context => new Sources.BranchCommits.BranchCommitsSource(api.Object, context, "owner", "repo", "main", "feature"));

        Assert.AreEqual(14 + 2 * PatternContractVerifier.CasesPerColumn * columns, api.Invocations.Count);
    }
}
