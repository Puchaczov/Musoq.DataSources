using Octokit;

namespace Musoq.DataSources.GitHub.Entities;

/// <summary>
///     Represents a GitHub pull request entity for querying.
/// </summary>
public class PullRequestEntity
{
    private readonly PullRequest _pullRequest;

    /// <summary>
    ///     Initializes a new instance of the PullRequestEntity class.
    /// </summary>
    /// <param name="pullRequest">The underlying Octokit pull request.</param>
    public PullRequestEntity(PullRequest pullRequest)
    {
        _pullRequest = pullRequest;
    }

    /// <summary>
    ///     Gets the pull request ID.
    /// </summary>
    public long Id => _pullRequest.Id;

    /// <summary>
    ///     Gets the pull request number.
    /// </summary>
    public int Number => _pullRequest.Number;

    /// <summary>
    ///     Gets the pull request title.
    /// </summary>
    public string Title => _pullRequest.Title;

    /// <summary>
    ///     Gets the pull request body/description.
    /// </summary>
    public string? Body => _pullRequest.Body;

    /// <summary>
    ///     Gets the pull request state (open/closed).
    /// </summary>
    public string State => _pullRequest.State.StringValue;

    /// <summary>
    ///     Gets the pull request URL.
    /// </summary>
    public string Url => _pullRequest.HtmlUrl;

    /// <summary>
    ///     Gets the author's login name.
    /// </summary>
    public string AuthorLogin => _pullRequest.User?.Login ?? string.Empty;

    /// <summary>
    ///     Gets the author's ID.
    /// </summary>
    public long? AuthorId => _pullRequest.User?.Id;

    /// <summary>
    ///     Gets the assignee's login name.
    /// </summary>
    public string? AssigneeLogin => _pullRequest.Assignee?.Login;

    /// <summary>
    ///     Gets all assignees' login names as comma-separated string.
    /// </summary>
    public string Assignees => string.Join(", ", _pullRequest.Assignees?.Select(a => a.Login) ?? []);

    /// <summary>
    ///     Gets the labels as comma-separated string.
    /// </summary>
    public string Labels => string.Join(", ", _pullRequest.Labels?.Select(l => l.Name) ?? []);

    /// <summary>
    ///     Gets the label names.
    /// </summary>
    public IReadOnlyList<string> LabelNames => _pullRequest.Labels?.Select(l => l.Name).ToList() ?? [];

    /// <summary>
    ///     Gets the milestone title.
    /// </summary>
    public string? MilestoneTitle => _pullRequest.Milestone?.Title;

    /// <summary>
    ///     Gets the milestone number.
    /// </summary>
    public int? MilestoneNumber => _pullRequest.Milestone?.Number;

    /// <summary>
    ///     Gets the source branch name.
    /// </summary>
    public string HeadRef => _pullRequest.Head?.Ref ?? string.Empty;

    /// <summary>
    ///     Gets the source branch SHA.
    /// </summary>
    public string HeadSha => _pullRequest.Head?.Sha ?? string.Empty;

    /// <summary>
    ///     Gets the source repository full name.
    /// </summary>
    public string? HeadRepository => _pullRequest.Head?.Repository?.FullName;

    /// <summary>
    ///     Gets the target branch name.
    /// </summary>
    public string BaseRef => _pullRequest.Base?.Ref ?? string.Empty;

    /// <summary>
    ///     Gets the target branch SHA.
    /// </summary>
    public string BaseSha => _pullRequest.Base?.Sha ?? string.Empty;

    /// <summary>
    ///     Gets the target repository full name.
    /// </summary>
    public string? BaseRepository => _pullRequest.Base?.Repository?.FullName;

    /// <summary>
    ///     Gets whether the pull request is merged.
    /// </summary>
    public bool Merged => _pullRequest.Merged;

    /// <summary>
    ///     Gets whether the pull request is mergeable.
    /// </summary>
    public bool? Mergeable => _pullRequest.Mergeable;

    /// <summary>
    ///     Gets the mergeable state.
    /// </summary>
    public string? MergeableState => _pullRequest.MergeableState?.StringValue;

    /// <summary>
    ///     Gets the user who merged the pull request.
    /// </summary>
    public string? MergedByLogin => _pullRequest.MergedBy?.Login;

    /// <summary>
    ///     Gets the merge commit SHA.
    /// </summary>
    public string? MergeCommitSha => _pullRequest.MergeCommitSha;

    /// <summary>
    ///     Gets the number of comments.
    /// </summary>
    public int Comments => _pullRequest.Comments;

    /// <summary>
    ///     Gets the number of commits.
    /// </summary>
    public int Commits => _pullRequest.Commits;

    /// <summary>
    ///     Gets the number of additions.
    /// </summary>
    public int Additions => _pullRequest.Additions;

    /// <summary>
    ///     Gets the number of deletions.
    /// </summary>
    public int Deletions => _pullRequest.Deletions;

    /// <summary>
    ///     Gets the number of changed files.
    /// </summary>
    public int ChangedFiles => _pullRequest.ChangedFiles;

    /// <summary>
    ///     Gets whether the pull request is a draft.
    /// </summary>
    public bool Draft => _pullRequest.Draft;

    /// <summary>
    ///     Gets the creation date.
    /// </summary>
    public DateTimeOffset CreatedAt => _pullRequest.CreatedAt;

    /// <summary>
    ///     Gets the last update date.
    /// </summary>
    public DateTimeOffset UpdatedAt => _pullRequest.UpdatedAt;

    /// <summary>
    ///     Gets the closed date.
    /// </summary>
    public DateTimeOffset? ClosedAt => _pullRequest.ClosedAt;

    /// <summary>
    ///     Gets the merged date.
    /// </summary>
    public DateTimeOffset? MergedAt => _pullRequest.MergedAt;

    /// <summary>
    ///     Gets whether the pull request is locked.
    /// </summary>
    public bool Locked => _pullRequest.Locked;

    /// <summary>
    ///     Gets the lock reason.
    /// </summary>
    public string? ActiveLockReason => _pullRequest.ActiveLockReason?.StringValue;
}