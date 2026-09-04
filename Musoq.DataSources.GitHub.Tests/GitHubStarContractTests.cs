using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.DataSources.GitHub;
using Musoq.DataSources.GitHub.Entities;
using Musoq.DataSources.GitHub.Tests.TestHelpers;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Evaluator.Tables;
using Musoq.Schema;
using Musoq.Schema.Optimization;
using Octokit;

namespace Musoq.DataSources.GitHub.Tests;

[TestClass]
public sealed class GitHubStarContractTests
{
    private static readonly StarContractCase[] Cases =
    [
        new(
            "repositories",
            [],
            [],
            "select * from github.repositories()",
            [
                Column("Id", typeof(long)), Column("Name", typeof(string)), Column("FullName", typeof(string)),
                Column("Description", typeof(string)), Column("Url", typeof(string)), Column("CloneUrl", typeof(string)),
                Column("SshUrl", typeof(string)), Column("DefaultBranch", typeof(string)), Column("IsPrivate", typeof(bool)),
                Column("IsFork", typeof(bool)), Column("IsArchived", typeof(bool)), Column("Language", typeof(string)),
                Column("ForksCount", typeof(int)), Column("StargazersCount", typeof(int)), Column("WatchersCount", typeof(int)),
                Column("OpenIssuesCount", typeof(int)), Column("Size", typeof(long)), Column("CreatedAt", typeof(DateTimeOffset)),
                Column("UpdatedAt", typeof(DateTimeOffset)), Column("PushedAt", typeof(DateTimeOffset?)),
                Column("OwnerLogin", typeof(string)), Column("License", typeof(string)), Column("HasIssues", typeof(bool)),
                Column("HasWiki", typeof(bool)), Column("HasDownloads", typeof(bool)), Column("Visibility", typeof(string))
            ],
            ["Topics"]),
        new(
            "repositories",
            [typeof(string)],
            ["testowner"],
            "select * from github.repositories('testowner')",
            [
                Column("Id", typeof(long)), Column("Name", typeof(string)), Column("FullName", typeof(string)),
                Column("Description", typeof(string)), Column("Url", typeof(string)), Column("CloneUrl", typeof(string)),
                Column("SshUrl", typeof(string)), Column("DefaultBranch", typeof(string)), Column("IsPrivate", typeof(bool)),
                Column("IsFork", typeof(bool)), Column("IsArchived", typeof(bool)), Column("Language", typeof(string)),
                Column("ForksCount", typeof(int)), Column("StargazersCount", typeof(int)), Column("WatchersCount", typeof(int)),
                Column("OpenIssuesCount", typeof(int)), Column("Size", typeof(long)), Column("CreatedAt", typeof(DateTimeOffset)),
                Column("UpdatedAt", typeof(DateTimeOffset)), Column("PushedAt", typeof(DateTimeOffset?)),
                Column("OwnerLogin", typeof(string)), Column("License", typeof(string)), Column("HasIssues", typeof(bool)),
                Column("HasWiki", typeof(bool)), Column("HasDownloads", typeof(bool)), Column("Visibility", typeof(string))
            ],
            ["Topics"]),
        new(
            "issues",
            [typeof(string), typeof(string)],
            ["testowner", "testrepo"],
            "select * from github.issues('testowner', 'testrepo')",
            [
                Column("Id", typeof(long)), Column("Number", typeof(int)), Column("Title", typeof(string)),
                Column("Body", typeof(string)), Column("State", typeof(string)), Column("Url", typeof(string)),
                Column("AuthorLogin", typeof(string)), Column("AuthorId", typeof(long?)),
                Column("AssigneeLogin", typeof(string)), Column("Assignees", typeof(string)), Column("Labels", typeof(string)),
                Column("MilestoneTitle", typeof(string)), Column("MilestoneNumber", typeof(int?)), Column("Comments", typeof(int)),
                Column("IsPullRequest", typeof(bool)), Column("CreatedAt", typeof(DateTimeOffset)),
                Column("UpdatedAt", typeof(DateTimeOffset?)), Column("ClosedAt", typeof(DateTimeOffset?)),
                Column("ClosedByLogin", typeof(string)), Column("Locked", typeof(bool)),
                Column("ActiveLockReason", typeof(string)), Column("RepositoryUrl", typeof(string)),
                Column("StateReason", typeof(string))
            ],
            ["LabelNames"]),
        new(
            "pullrequests",
            [typeof(string), typeof(string)],
            ["testowner", "testrepo"],
            "select * from github.pullrequests('testowner', 'testrepo')",
            [
                Column("Id", typeof(long)), Column("Number", typeof(int)), Column("Title", typeof(string)),
                Column("Body", typeof(string)), Column("State", typeof(string)), Column("Url", typeof(string)),
                Column("AuthorLogin", typeof(string)), Column("AuthorId", typeof(long?)),
                Column("AssigneeLogin", typeof(string)), Column("Assignees", typeof(string)), Column("Labels", typeof(string)),
                Column("MilestoneTitle", typeof(string)), Column("MilestoneNumber", typeof(int?)),
                Column("HeadRef", typeof(string)), Column("HeadSha", typeof(string)), Column("HeadRepository", typeof(string)),
                Column("BaseRef", typeof(string)), Column("BaseSha", typeof(string)), Column("BaseRepository", typeof(string)),
                Column("Merged", typeof(bool)), Column("Mergeable", typeof(bool?)), Column("MergeableState", typeof(string)),
                Column("MergedByLogin", typeof(string)), Column("MergeCommitSha", typeof(string)),
                Column("Comments", typeof(int)), Column("Commits", typeof(int)), Column("Additions", typeof(int)),
                Column("Deletions", typeof(int)), Column("ChangedFiles", typeof(int)), Column("Draft", typeof(bool)),
                Column("CreatedAt", typeof(DateTimeOffset)), Column("UpdatedAt", typeof(DateTimeOffset)),
                Column("ClosedAt", typeof(DateTimeOffset?)), Column("MergedAt", typeof(DateTimeOffset?)),
                Column("Locked", typeof(bool)), Column("ActiveLockReason", typeof(string))
            ],
            ["LabelNames"]),
        new(
            "commits",
            [typeof(string), typeof(string)],
            ["testowner", "testrepo"],
            "select * from github.commits('testowner', 'testrepo')",
            CommitColumns(),
            []),
        new(
            "commits",
            [typeof(string), typeof(string), typeof(string)],
            ["testowner", "testrepo", "main"],
            "select * from github.commits('testowner', 'testrepo', 'main')",
            CommitColumns(),
            []),
        new(
            "branchcommits",
            [typeof(string), typeof(string), typeof(string), typeof(string)],
            ["testowner", "testrepo", "main", "feature"],
            "select * from github.branchcommits('testowner', 'testrepo', 'main', 'feature')",
            CommitColumns(),
            []),
        new(
            "branches",
            [typeof(string), typeof(string)],
            ["testowner", "testrepo"],
            "select * from github.branches('testowner', 'testrepo')",
            [
                Column("Name", typeof(string)), Column("CommitSha", typeof(string)), Column("CommitUrl", typeof(string)),
                Column("Protected", typeof(bool)), Column("RepositoryOwner", typeof(string)), Column("RepositoryName", typeof(string))
            ],
            []),
        new(
            "releases",
            [typeof(string), typeof(string)],
            ["testowner", "testrepo"],
            "select * from github.releases('testowner', 'testrepo')",
            [
                Column("Id", typeof(long)), Column("TagName", typeof(string)), Column("Name", typeof(string)),
                Column("Body", typeof(string)), Column("Url", typeof(string)), Column("TargetCommitish", typeof(string)),
                Column("Draft", typeof(bool)), Column("Prerelease", typeof(bool)), Column("AuthorLogin", typeof(string)),
                Column("AuthorId", typeof(long?)), Column("CreatedAt", typeof(DateTimeOffset)),
                Column("PublishedAt", typeof(DateTimeOffset?)), Column("AssetsCount", typeof(int)),
                Column("TarballUrl", typeof(string)), Column("ZipballUrl", typeof(string))
            ],
            [])
    ];

    [TestMethod]
    public void EveryGitHubConstructor_HasOneExactStarContract()
    {
        var api = CreateApi();
        var schema = new GitHubSchema(api.Object);
        var context = CreateMetadataContext();

        StarContractAssertions.AssertConstructors(schema.GetRawConstructors(context), Cases);

        foreach (var contract in Cases)
        {
            var table = schema.GetTableByName(contract.MethodName, context, contract.Arguments.ToArray());
            StarContractAssertions.AssertExcludedColumnsRemainInSchema(table, contract);

            var result = Compile(contract.Query, api.Object).Run();
            StarContractAssertions.AssertResult(result, contract);
        }
    }

    [TestMethod]
    public void MarkedCollections_AreCrossApplyAddressable()
    {
        var api = CreateApi();
        var queries = new Dictionary<string, (string Query, string ExpectedValue)>
        {
            ["repository topics"] = ("select t.Value from github.repositories() r cross apply r.Topics t", "migration"),
            ["issue labels"] = ("select l.Value from github.issues('testowner', 'testrepo') i cross apply i.LabelNames l", "bug"),
            ["pull request labels"] = ("select l.Value from github.pullrequests('testowner', 'testrepo') p cross apply p.LabelNames l", "enhancement")
        };

        foreach (var pair in queries)
        {
            var result = Compile(pair.Value.Query, api.Object).Run();
            Assert.AreEqual(1, result.Count, $"GitHub apply '{pair.Key}' returned an unexpected row count.");
            Assert.AreEqual(pair.Value.ExpectedValue, result[0][0]);
        }
    }

    private static Mock<IGitHubApi> CreateApi()
    {
        var api = new Mock<IGitHubApi>();
        var repository = MockEntityFactory.CreateRepository(topics: ["migration"]);
        var issue = MockEntityFactory.CreateIssue(labels: ["bug"]);
        var pullRequest = MockEntityFactory.CreatePullRequest(labels: ["enhancement"]);
        var commit = MockEntityFactory.CreateCommit();
        var branch = MockEntityFactory.CreateBranch();
        var release = MockEntityFactory.CreateRelease();

        api.Setup(value => value.GetUserRepositoriesAsync(
                It.IsAny<RepositoryRequest>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync([repository]);
        api.Setup(value => value.GetRepositoriesForOwnerAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync([repository]);
        api.Setup(value => value.GetIssuesAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RepositoryIssueRequest>(),
                It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync([issue]);
        api.Setup(value => value.GetPullRequestsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<PullRequestRequest>(),
                It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync([pullRequest]);
        api.Setup(value => value.GetCommitsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CommitRequest>(),
                It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync([commit]);
        api.Setup(value => value.GetBranchSpecificCommitsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync([commit]);
        api.Setup(value => value.GetBranchesAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync([branch]);
        api.Setup(value => value.GetReleasesAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync([release]);

        return api;
    }

    private static CompiledQuery Compile(string query, IGitHubApi api)
    {
        var provider = new Mock<ISchemaProvider>();
        provider.Setup(value => value.GetSchema(It.IsAny<string>())).Returns(new GitHubSchema(api));

        return InstanceCreatorHelpers.CompileForExecution(
            query,
            Guid.NewGuid().ToString(),
            provider.Object,
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    private static SourceMetadataContext CreateMetadataContext()
    {
        return new SourceMetadataContext(
            "github-star-contract",
            CancellationToken.None,
            [],
            new Dictionary<string, string>(),
            NullLogger.Instance);
    }

    private static StarContractColumn[] CommitColumns()
    {
        return
        [
            Column("Sha", typeof(string)), Column("ShortSha", typeof(string)), Column("Message", typeof(string)),
            Column("Url", typeof(string)), Column("AuthorName", typeof(string)), Column("AuthorEmail", typeof(string)),
            Column("AuthorLogin", typeof(string)), Column("AuthorId", typeof(long?)), Column("AuthorDate", typeof(DateTimeOffset?)),
            Column("CommitterName", typeof(string)), Column("CommitterEmail", typeof(string)),
            Column("CommitterLogin", typeof(string)), Column("CommitterId", typeof(long?)),
            Column("CommitterDate", typeof(DateTimeOffset?)), Column("Additions", typeof(int)), Column("Deletions", typeof(int)),
            Column("Total", typeof(int)), Column("ParentShas", typeof(string)), Column("ParentCount", typeof(int)),
            Column("CommentCount", typeof(int)), Column("Verified", typeof(bool?)), Column("VerificationReason", typeof(string)),
            Column("FilesChanged", typeof(int))
        ];
    }

    private static StarContractColumn Column(string name, Type type) => new(name, type);
}
