using Octokit;
using Musoq.Plugins.Attributes;

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
    public string Title => ReadTitle(_pullRequest);

    /// <summary>
    ///     Gets the pull request body/description.
    /// </summary>
    public string? Body => ReadBody(_pullRequest);

    /// <summary>
    ///     Gets the pull request state (open/closed).
    /// </summary>
    public string State => ReadState(_pullRequest);

    /// <summary>
    ///     Gets the pull request URL.
    /// </summary>
    public string Url => ReadUrl(_pullRequest);

    /// <summary>
    ///     Gets the author's login name.
    /// </summary>
    public string AuthorLogin => ReadAuthorLogin(_pullRequest);

    /// <summary>
    ///     Gets the author's ID.
    /// </summary>
    public long? AuthorId => _pullRequest.User?.Id;

    /// <summary>
    ///     Gets the assignee's login name.
    /// </summary>
    public string? AssigneeLogin => ReadAssigneeLogin(_pullRequest);

    /// <summary>
    ///     Gets all assignees' login names as comma-separated string.
    /// </summary>
    public string Assignees => ReadAssignees(_pullRequest);

    /// <summary>
    ///     Gets the labels as comma-separated string.
    /// </summary>
    public string Labels => ReadLabels(_pullRequest);

    /// <summary>
    ///     Gets the label names.
    /// </summary>
    [BindablePropertyAsTable]
    public IReadOnlyList<string> LabelNames => _pullRequest.Labels?.Select(l => l.Name).ToList() ?? [];

    /// <summary>
    ///     Gets the milestone title.
    /// </summary>
    public string? MilestoneTitle => ReadMilestoneTitle(_pullRequest);

    /// <summary>
    ///     Gets the milestone number.
    /// </summary>
    public int? MilestoneNumber => _pullRequest.Milestone?.Number;

    /// <summary>
    ///     Gets the source branch name.
    /// </summary>
    public string HeadRef => ReadHeadRef(_pullRequest);

    /// <summary>
    ///     Gets the source branch SHA.
    /// </summary>
    public string HeadSha => ReadHeadSha(_pullRequest);

    /// <summary>
    ///     Gets the source repository full name.
    /// </summary>
    public string? HeadRepository => ReadHeadRepository(_pullRequest);

    /// <summary>
    ///     Gets the target branch name.
    /// </summary>
    public string BaseRef => ReadBaseRef(_pullRequest);

    /// <summary>
    ///     Gets the target branch SHA.
    /// </summary>
    public string BaseSha => ReadBaseSha(_pullRequest);

    /// <summary>
    ///     Gets the target repository full name.
    /// </summary>
    public string? BaseRepository => ReadBaseRepository(_pullRequest);

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
    public string? MergeableState => ReadMergeableState(_pullRequest);

    /// <summary>
    ///     Gets the user who merged the pull request.
    /// </summary>
    public string? MergedByLogin => ReadMergedByLogin(_pullRequest);

    /// <summary>
    ///     Gets the merge commit SHA.
    /// </summary>
    public string? MergeCommitSha => ReadMergeCommitSha(_pullRequest);

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
    public string? ActiveLockReason => ReadActiveLockReason(_pullRequest);

    internal static string ReadTitle(PullRequest value) => value.Title;
    internal static string? ReadBody(PullRequest value) => value.Body;
    internal static string ReadState(PullRequest value) => value.State.StringValue;
    internal static string ReadUrl(PullRequest value) => value.HtmlUrl;
    internal static string ReadAuthorLogin(PullRequest value) => value.User?.Login ?? string.Empty;
    internal static string? ReadAssigneeLogin(PullRequest value) => value.Assignee?.Login;
    internal static string ReadAssignees(PullRequest value) => string.Join(", ", value.Assignees?.Select(a => a.Login) ?? []);
    internal static string ReadLabels(PullRequest value) => string.Join(", ", value.Labels?.Select(l => l.Name) ?? []);
    internal static string? ReadMilestoneTitle(PullRequest value) => value.Milestone?.Title;
    internal static string ReadHeadRef(PullRequest value) => value.Head?.Ref ?? string.Empty;
    internal static string ReadHeadSha(PullRequest value) => value.Head?.Sha ?? string.Empty;
    internal static string? ReadHeadRepository(PullRequest value) => value.Head?.Repository?.FullName;
    internal static string ReadBaseRef(PullRequest value) => value.Base?.Ref ?? string.Empty;
    internal static string ReadBaseSha(PullRequest value) => value.Base?.Sha ?? string.Empty;
    internal static string? ReadBaseRepository(PullRequest value) => value.Base?.Repository?.FullName;
    internal static string? ReadMergeableState(PullRequest value) => value.MergeableState?.StringValue;
    internal static string? ReadMergedByLogin(PullRequest value) => value.MergedBy?.Login;
    internal static string? ReadMergeCommitSha(PullRequest value) => value.MergeCommitSha;
    internal static string? ReadActiveLockReason(PullRequest value) => value.ActiveLockReason?.StringValue;
}
