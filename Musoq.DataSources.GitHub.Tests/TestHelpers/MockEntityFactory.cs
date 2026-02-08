using System;
using System.Collections.Generic;
using Musoq.DataSources.GitHub.Entities;
using Octokit;

namespace Musoq.DataSources.GitHub.Tests.TestHelpers;

/// <summary>
/// Factory for creating mock entities for testing.
/// Uses Octokit's public constructors to create properly initialized objects.
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
        var owner = CreateUser(login: ownerLogin, id: 1);
        
        var repo = new Repository(
            url: $"https://api.github.com/repos/{fullName}",
            htmlUrl: $"https://github.com/{fullName}",
            cloneUrl: $"https://github.com/{fullName}.git",
            gitUrl: $"git://github.com/{fullName}.git",
            sshUrl: $"git@github.com:{fullName}.git",
            svnUrl: $"https://github.com/{fullName}",
            mirrorUrl: null,
            archiveUrl: $"https://api.github.com/repos/{fullName}/{{archive_format}}{{/ref}}",
            id: id,
            nodeId: $"node_{id}",
            owner: owner,
            name: name,
            fullName: fullName,
            isTemplate: false,
            description: description,
            homepage: null,
            language: language,
            @private: isPrivate,
            fork: isFork,
            forksCount: forksCount,
            stargazersCount: stargazersCount,
            defaultBranch: defaultBranch,
            openIssuesCount: 0,
            pushedAt: now,
            createdAt: createdAt ?? now,
            updatedAt: updatedAt ?? now,
            permissions: null,
            parent: null,
            source: null,
            license: null,
            hasDiscussions: false,
            hasIssues: true,
            hasWiki: true,
            hasDownloads: true,
            hasPages: false,
            subscribersCount: 0,
            size: 1000,
            allowRebaseMerge: true,
            allowSquashMerge: true,
            allowMergeCommit: true,
            archived: isArchived,
            watchersCount: 0,
            deleteBranchOnMerge: false,
            visibility: isPrivate ? RepositoryVisibility.Private : RepositoryVisibility.Public,
            topics: new List<string>(),
            allowAutoMerge: false,
            allowUpdateBranch: null,
            webCommitSignoffRequired: null,
            securityAndAnalysis: null
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
        var user = CreateUser(login: authorLogin, id: 1);
        var itemState = state.ToLower() == "open" ? ItemState.Open : ItemState.Closed;
        
        var issue = new Issue(
            url: $"https://api.github.com/repos/owner/repo/issues/{number}",
            htmlUrl: $"https://github.com/owner/repo/issues/{number}",
            commentsUrl: $"https://api.github.com/repos/owner/repo/issues/{number}/comments",
            eventsUrl: $"https://api.github.com/repos/owner/repo/issues/{number}/events",
            number: number,
            state: itemState,
            title: title,
            body: body,
            closedBy: null,
            user: user,
            labels: new List<Label>(),
            assignee: null,
            assignees: new List<User>(),
            milestone: null,
            comments: 0,
            pullRequest: null,
            closedAt: closedAt,
            createdAt: createdAt ?? now,
            updatedAt: updatedAt ?? now,
            id: id,
            nodeId: $"node_{id}",
            locked: false,
            repository: null,
            reactions: null,
            activeLockReason: null,
            stateReason: null
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
            name: authorName ?? "Unknown",
            email: authorEmail ?? "unknown@example.com",
            date: date
        );
        
        var innerCommit = new Commit(
            nodeId: "node_commit",
            url: $"https://api.github.com/repos/owner/repo/git/commits/{sha}",
            label: null,
            @ref: null,
            sha: sha,
            user: null,
            repository: null,
            message: message,
            author: committer,
            committer: committer,
            tree: null,
            parents: new List<GitReference>(),
            commentCount: 0,
            verification: null
        );
        
        var gitHubAuthor = authorLogin != null ? CreateAuthor(login: authorLogin, id: 1) : null;
        
        var stats = new GitHubCommitStats(additions, deletions, additions + deletions);
        
        var commit = new GitHubCommit(
            nodeId: "node_commit",
            url: $"https://api.github.com/repos/owner/repo/commits/{sha}",
            label: null,
            @ref: null,
            sha: sha,
            user: null,
            repository: null,
            author: gitHubAuthor,
            commentsUrl: $"https://api.github.com/repos/owner/repo/commits/{sha}/comments",
            commit: innerCommit,
            committer: gitHubAuthor,
            htmlUrl: $"https://github.com/owner/repo/commit/{sha}",
            stats: stats,
            parents: new List<GitReference>(),
            files: new List<GitHubCommitFile>()
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
            nodeId: "node_ref",
            url: $"https://api.github.com/repos/{owner}/{repo}/git/refs/heads/{name}",
            label: null,
            @ref: $"refs/heads/{name}",
            sha: sha,
            user: null,
            repository: null
        );
        
        var branch = new Branch(
            name: name,
            commit: gitRef,
            @protected: isProtected
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
        var author = CreateAuthor(login: authorLogin, id: 1);
        
        var release = new Release(
            url: $"https://api.github.com/repos/owner/repo/releases/{id}",
            htmlUrl: $"https://github.com/owner/repo/releases/tag/{tagName}",
            assetsUrl: $"https://api.github.com/repos/owner/repo/releases/{id}/assets",
            uploadUrl: $"https://uploads.github.com/repos/owner/repo/releases/{id}/assets",
            id: id,
            nodeId: $"node_{id}",
            tagName: tagName,
            targetCommitish: "main",
            name: name,
            body: body,
            draft: draft,
            prerelease: prerelease,
            createdAt: createdAt ?? now,
            publishedAt: publishedAt ?? now,
            author: author,
            tarballUrl: $"https://github.com/owner/repo/archive/{tagName}.tar.gz",
            zipballUrl: $"https://github.com/owner/repo/archive/{tagName}.zip",
            assets: new List<ReleaseAsset>()
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
        var user = CreateUser(login: authorLogin, id: 1);
        var itemState = state.ToLower() == "open" ? ItemState.Open : ItemState.Closed;
        
        var head = new GitReference(
            nodeId: "node_head",
            url: null,
            label: null,
            @ref: headRef,
            sha: "abc123",
            user: null,
            repository: null
        );
        
        var baseGitRef = new GitReference(
            nodeId: "node_base",
            url: null,
            label: null,
            @ref: baseRef,
            sha: "def456",
            user: null,
            repository: null
        );
        
        // Merged is computed from MergedAt.HasValue
        var effectiveMergedAt = merged ? (mergedAt ?? now) : (DateTimeOffset?)null;
        
        var pr = new PullRequest(
            id: id,
            nodeId: $"node_{id}",
            url: $"https://api.github.com/repos/owner/repo/pulls/{number}",
            htmlUrl: $"https://github.com/owner/repo/pull/{number}",
            diffUrl: $"https://github.com/owner/repo/pull/{number}.diff",
            patchUrl: $"https://github.com/owner/repo/pull/{number}.patch",
            issueUrl: $"https://api.github.com/repos/owner/repo/issues/{number}",
            statusesUrl: $"https://api.github.com/repos/owner/repo/statuses/abc123",
            number: number,
            state: itemState,
            title: title,
            body: body,
            createdAt: createdAt ?? now,
            updatedAt: updatedAt ?? now,
            closedAt: closedAt,
            mergedAt: effectiveMergedAt,
            head: head,
            @base: baseGitRef,
            user: user,
            assignee: null,
            assignees: new List<User>(),
            draft: draft,
            mergeable: null,
            mergeableState: null,
            mergedBy: null,
            mergeCommitSha: null,
            comments: 0,
            commits: 1,
            additions: 10,
            deletions: 5,
            changedFiles: 2,
            milestone: null,
            locked: false,
            maintainerCanModify: null,
            requestedReviewers: new List<User>(),
            requestedTeams: new List<Team>(),
            labels: new List<Label>(),
            activeLockReason: null
        );

        return new PullRequestEntity(pr);
    }

    /// <summary>
    /// Creates a User with the specified login and id.
    /// </summary>
    private static User CreateUser(string login, long id)
    {
        var now = DateTimeOffset.UtcNow;
        return new User(
            avatarUrl: $"https://avatars.githubusercontent.com/u/{id}",
            bio: null,
            blog: null,
            collaborators: 0,
            company: null,
            createdAt: now,
            updatedAt: now,
            diskUsage: 0,
            email: $"{login}@example.com",
            followers: 0,
            following: 0,
            hireable: null,
            htmlUrl: $"https://github.com/{login}",
            totalPrivateRepos: 0,
            id: id,
            location: null,
            login: login,
            name: login,
            nodeId: $"node_user_{id}",
            ownedPrivateRepos: 0,
            plan: null,
            privateGists: 0,
            publicGists: 0,
            publicRepos: 0,
            url: $"https://api.github.com/users/{login}",
            permissions: null,
            siteAdmin: false,
            ldapDistinguishedName: null,
            suspendedAt: null
        );
    }

    /// <summary>
    /// Creates an Author with the specified login and id.
    /// </summary>
    private static Author CreateAuthor(string login, long id)
    {
        return new Author(
            login: login,
            id: id,
            nodeId: $"node_author_{id}",
            avatarUrl: $"https://avatars.githubusercontent.com/u/{id}",
            url: $"https://api.github.com/users/{login}",
            htmlUrl: $"https://github.com/{login}",
            followersUrl: $"https://api.github.com/users/{login}/followers",
            followingUrl: $"https://api.github.com/users/{login}/following{{/other_user}}",
            gistsUrl: $"https://api.github.com/users/{login}/gists{{/gist_id}}",
            type: "User",
            starredUrl: $"https://api.github.com/users/{login}/starred{{/owner}}{{/repo}}",
            subscriptionsUrl: $"https://api.github.com/users/{login}/subscriptions",
            organizationsUrl: $"https://api.github.com/users/{login}/orgs",
            reposUrl: $"https://api.github.com/users/{login}/repos",
            eventsUrl: $"https://api.github.com/users/{login}/events{{/privacy}}",
            receivedEventsUrl: $"https://api.github.com/users/{login}/received_events",
            siteAdmin: false
        );
    }
}
