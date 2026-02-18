using System;
using System.Collections.Generic;
using Musoq.DataSources.GitHub.Entities;
using Octokit;

namespace Musoq.DataSources.GitHub.Tests.TestHelpers;

/// <summary>
///     Factory for creating mock entities for testing.
///     Uses Octokit's public constructors to create properly initialized objects.
/// </summary>
internal static class MockEntityFactory
{
    public static RepositoryEntity CreateRepository(
        long id = 1,
        string name = "test-repo",
        string fullName = "owner/test-repo",
        string? description = null,
        string? language = null,
        int stargazersCount = 0,
        int forksCount = 0,
        bool isPrivate = false,
        bool isFork = false,
        bool isArchived = false,
        string? defaultBranch = "main",
        string ownerLogin = "owner",
        DateTimeOffset? createdAt = null,
        DateTimeOffset? updatedAt = null)
    {
        var now = DateTimeOffset.UtcNow;
        var owner = CreateUser(ownerLogin, 1);

        var repo = new Repository(
            $"https://api.github.com/repos/{fullName}",
            $"https://github.com/{fullName}",
            $"https://github.com/{fullName}.git",
            $"git://github.com/{fullName}.git",
            $"git@github.com:{fullName}.git",
            $"https://github.com/{fullName}",
            null,
            $"https://api.github.com/repos/{fullName}/{{archive_format}}{{/ref}}",
            id,
            $"node_{id}",
            owner,
            name,
            fullName,
            false,
            description,
            null,
            language,
            isPrivate,
            isFork,
            forksCount,
            stargazersCount,
            defaultBranch,
            0,
            now,
            createdAt ?? now,
            updatedAt ?? now,
            null,
            null,
            null,
            null,
            false,
            true,
            true,
            true,
            false,
            0,
            1000,
            true,
            true,
            true,
            isArchived,
            0,
            false,
            isPrivate ? RepositoryVisibility.Private : RepositoryVisibility.Public,
            new List<string>(),
            false,
            null,
            null,
            null
        );

        return new RepositoryEntity(repo);
    }

    public static IssueEntity CreateIssue(
        int id = 1,
        int number = 1,
        string title = "Test Issue",
        string? body = null,
        string state = "open",
        string authorLogin = "testuser",
        DateTimeOffset? createdAt = null,
        DateTimeOffset? updatedAt = null,
        DateTimeOffset? closedAt = null)
    {
        var now = DateTimeOffset.UtcNow;
        var user = CreateUser(authorLogin, 1);
        var itemState = state.ToLower() == "open" ? ItemState.Open : ItemState.Closed;

        var issue = new Issue(
            $"https://api.github.com/repos/owner/repo/issues/{number}",
            $"https://github.com/owner/repo/issues/{number}",
            $"https://api.github.com/repos/owner/repo/issues/{number}/comments",
            $"https://api.github.com/repos/owner/repo/issues/{number}/events",
            number,
            itemState,
            title,
            body,
            null,
            user,
            new List<Label>(),
            null,
            new List<User>(),
            null,
            0,
            null,
            closedAt,
            createdAt ?? now,
            updatedAt ?? now,
            id,
            $"node_{id}",
            false,
            null,
            null,
            null,
            null
        );

        return new IssueEntity(issue);
    }

    public static CommitEntity CreateCommit(
        string sha = "abc123def456",
        string message = "Test commit message",
        string? authorName = "Test Author",
        string? authorEmail = "test@example.com",
        string? authorLogin = "testauthor",
        DateTimeOffset? authorDate = null,
        int additions = 10,
        int deletions = 5)
    {
        var now = DateTimeOffset.UtcNow;
        var date = authorDate ?? now;

        var committer = new Committer(
            authorName ?? "Unknown",
            authorEmail ?? "unknown@example.com",
            date
        );

        var innerCommit = new Commit(
            "node_commit",
            $"https://api.github.com/repos/owner/repo/git/commits/{sha}",
            null,
            null,
            sha,
            null,
            null,
            message,
            committer,
            committer,
            null,
            new List<GitReference>(),
            0,
            null
        );

        var gitHubAuthor = authorLogin != null ? CreateAuthor(authorLogin, 1) : null;

        var stats = new GitHubCommitStats(additions, deletions, additions + deletions);

        var commit = new GitHubCommit(
            "node_commit",
            $"https://api.github.com/repos/owner/repo/commits/{sha}",
            null,
            null,
            sha,
            null,
            null,
            gitHubAuthor,
            $"https://api.github.com/repos/owner/repo/commits/{sha}/comments",
            innerCommit,
            gitHubAuthor,
            $"https://github.com/owner/repo/commit/{sha}",
            stats,
            new List<GitReference>(),
            new List<GitHubCommitFile>()
        );

        return new CommitEntity(commit);
    }

    public static BranchEntity CreateBranch(
        string name = "main",
        string sha = "abc123",
        bool isProtected = false,
        string owner = "owner",
        string repo = "repo")
    {
        var gitRef = new GitReference(
            "node_ref",
            $"https://api.github.com/repos/{owner}/{repo}/git/refs/heads/{name}",
            null,
            $"refs/heads/{name}",
            sha,
            null,
            null
        );

        var branch = new Branch(
            name,
            gitRef,
            isProtected
        );

        return new BranchEntity(branch, owner, repo);
    }

    public static ReleaseEntity CreateRelease(
        int id = 1,
        string tagName = "v1.0.0",
        string name = "Release 1.0.0",
        string? body = null,
        bool draft = false,
        bool prerelease = false,
        string authorLogin = "releaseauthor",
        DateTimeOffset? createdAt = null,
        DateTimeOffset? publishedAt = null)
    {
        var now = DateTimeOffset.UtcNow;
        var author = CreateAuthor(authorLogin, 1);

        var release = new Release(
            $"https://api.github.com/repos/owner/repo/releases/{id}",
            $"https://github.com/owner/repo/releases/tag/{tagName}",
            $"https://api.github.com/repos/owner/repo/releases/{id}/assets",
            $"https://uploads.github.com/repos/owner/repo/releases/{id}/assets",
            id,
            $"node_{id}",
            tagName,
            "main",
            name,
            body,
            draft,
            prerelease,
            createdAt ?? now,
            publishedAt ?? now,
            author,
            $"https://github.com/owner/repo/archive/{tagName}.tar.gz",
            $"https://github.com/owner/repo/archive/{tagName}.zip",
            new List<ReleaseAsset>()
        );

        return new ReleaseEntity(release);
    }

    public static PullRequestEntity CreatePullRequest(
        long id = 1,
        int number = 1,
        string title = "Test PR",
        string? body = null,
        string state = "open",
        string authorLogin = "prauthor",
        string headRef = "feature",
        string baseRef = "main",
        bool merged = false,
        bool draft = false,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? updatedAt = null,
        DateTimeOffset? closedAt = null,
        DateTimeOffset? mergedAt = null)
    {
        var now = DateTimeOffset.UtcNow;
        var user = CreateUser(authorLogin, 1);
        var itemState = state.ToLower() == "open" ? ItemState.Open : ItemState.Closed;

        var head = new GitReference(
            "node_head",
            null,
            null,
            headRef,
            "abc123",
            null,
            null
        );

        var baseGitRef = new GitReference(
            "node_base",
            null,
            null,
            baseRef,
            "def456",
            null,
            null
        );


        var effectiveMergedAt = merged ? mergedAt ?? now : (DateTimeOffset?)null;

        var pr = new PullRequest(
            id,
            $"node_{id}",
            $"https://api.github.com/repos/owner/repo/pulls/{number}",
            $"https://github.com/owner/repo/pull/{number}",
            $"https://github.com/owner/repo/pull/{number}.diff",
            $"https://github.com/owner/repo/pull/{number}.patch",
            $"https://api.github.com/repos/owner/repo/issues/{number}",
            "https://api.github.com/repos/owner/repo/statuses/abc123",
            number,
            itemState,
            title,
            body,
            createdAt ?? now,
            updatedAt ?? now,
            closedAt,
            effectiveMergedAt,
            head,
            baseGitRef,
            user,
            null,
            new List<User>(),
            draft,
            null,
            null,
            null,
            null,
            0,
            1,
            10,
            5,
            2,
            null,
            false,
            null,
            new List<User>(),
            new List<Team>(),
            new List<Label>(),
            null
        );

        return new PullRequestEntity(pr);
    }

    private static User CreateUser(string login, long id)
    {
        var now = DateTimeOffset.UtcNow;
        return new User(
            $"https://avatars.githubusercontent.com/u/{id}",
            null,
            null,
            0,
            null,
            now,
            now,
            0,
            $"{login}@example.com",
            0,
            0,
            null,
            $"https://github.com/{login}",
            0,
            id,
            null,
            login,
            login,
            $"node_user_{id}",
            0,
            null,
            0,
            0,
            0,
            $"https://api.github.com/users/{login}",
            null,
            false,
            null,
            null
        );
    }

    private static Author CreateAuthor(string login, long id)
    {
        return new Author(
            login,
            id,
            $"node_author_{id}",
            $"https://avatars.githubusercontent.com/u/{id}",
            $"https://api.github.com/users/{login}",
            $"https://github.com/{login}",
            $"https://api.github.com/users/{login}/followers",
            $"https://api.github.com/users/{login}/following{{/other_user}}",
            $"https://api.github.com/users/{login}/gists{{/gist_id}}",
            "User",
            $"https://api.github.com/users/{login}/starred{{/owner}}{{/repo}}",
            $"https://api.github.com/users/{login}/subscriptions",
            $"https://api.github.com/users/{login}/orgs",
            $"https://api.github.com/users/{login}/repos",
            $"https://api.github.com/users/{login}/events{{/privacy}}",
            $"https://api.github.com/users/{login}/received_events",
            false
        );
    }
}