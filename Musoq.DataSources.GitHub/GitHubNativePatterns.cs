#nullable enable
using Octokit;
using Musoq.DataSources.GitHub.Entities;
using Musoq.DataSources.Common;

namespace Musoq.DataSources.GitHub;

internal static class GitHubNativePatterns
{
    internal readonly record struct BranchInput(Branch Value, string Owner, string Repository);
    internal static readonly PatternColumnBindings<Repository> Repository = new(
        ("Name", static row => RepositoryEntity.ReadName(row)),
        ("FullName", static row => RepositoryEntity.ReadFullName(row)),
        ("Description", static row => RepositoryEntity.ReadDescription(row)),
        ("Url", static row => RepositoryEntity.ReadUrl(row)),
        ("CloneUrl", static row => RepositoryEntity.ReadCloneUrl(row)),
        ("SshUrl", static row => RepositoryEntity.ReadSshUrl(row)),
        ("DefaultBranch", static row => RepositoryEntity.ReadDefaultBranch(row)),
        ("Language", static row => RepositoryEntity.ReadLanguage(row)),
        ("OwnerLogin", static row => RepositoryEntity.ReadOwnerLogin(row)),
        ("License", static row => RepositoryEntity.ReadLicense(row)),
        ("Visibility", static row => RepositoryEntity.ReadVisibility(row))
    );

    internal static readonly PatternColumnBindings<Issue> Issue = new(
        ("Title", static row => IssueEntity.ReadTitle(row)),
        ("Body", static row => IssueEntity.ReadBody(row)),
        ("State", static row => IssueEntity.ReadState(row)),
        ("Url", static row => IssueEntity.ReadUrl(row)),
        ("AuthorLogin", static row => IssueEntity.ReadAuthorLogin(row)),
        ("AssigneeLogin", static row => IssueEntity.ReadAssigneeLogin(row)),
        ("Assignees", static row => IssueEntity.ReadAssignees(row)),
        ("Labels", static row => IssueEntity.ReadLabels(row)),
        ("MilestoneTitle", static row => IssueEntity.ReadMilestoneTitle(row)),
        ("ClosedByLogin", static row => IssueEntity.ReadClosedByLogin(row)),
        ("ActiveLockReason", static row => IssueEntity.ReadActiveLockReason(row)),
        ("RepositoryUrl", static row => IssueEntity.ReadRepositoryUrl(row)),
        ("StateReason", static row => IssueEntity.ReadStateReason(row))
    );

    internal static readonly PatternColumnBindings<PullRequest> PullRequest = new(
        ("Title", static row => PullRequestEntity.ReadTitle(row)),
        ("Body", static row => PullRequestEntity.ReadBody(row)),
        ("State", static row => PullRequestEntity.ReadState(row)),
        ("Url", static row => PullRequestEntity.ReadUrl(row)),
        ("AuthorLogin", static row => PullRequestEntity.ReadAuthorLogin(row)),
        ("AssigneeLogin", static row => PullRequestEntity.ReadAssigneeLogin(row)),
        ("Assignees", static row => PullRequestEntity.ReadAssignees(row)),
        ("Labels", static row => PullRequestEntity.ReadLabels(row)),
        ("MilestoneTitle", static row => PullRequestEntity.ReadMilestoneTitle(row)),
        ("HeadRef", static row => PullRequestEntity.ReadHeadRef(row)),
        ("HeadSha", static row => PullRequestEntity.ReadHeadSha(row)),
        ("HeadRepository", static row => PullRequestEntity.ReadHeadRepository(row)),
        ("BaseRef", static row => PullRequestEntity.ReadBaseRef(row)),
        ("BaseSha", static row => PullRequestEntity.ReadBaseSha(row)),
        ("BaseRepository", static row => PullRequestEntity.ReadBaseRepository(row)),
        ("MergeableState", static row => PullRequestEntity.ReadMergeableState(row)),
        ("MergedByLogin", static row => PullRequestEntity.ReadMergedByLogin(row)),
        ("MergeCommitSha", static row => PullRequestEntity.ReadMergeCommitSha(row)),
        ("ActiveLockReason", static row => PullRequestEntity.ReadActiveLockReason(row))
    );

    internal static readonly PatternColumnBindings<GitHubCommit> Commit = new(
        ("Sha", static row => CommitEntity.ReadSha(row)),
        ("ShortSha", static row => CommitEntity.ReadShortSha(row)),
        ("Message", static row => CommitEntity.ReadMessage(row)),
        ("Url", static row => CommitEntity.ReadUrl(row)),
        ("AuthorName", static row => CommitEntity.ReadAuthorName(row)),
        ("AuthorEmail", static row => CommitEntity.ReadAuthorEmail(row)),
        ("AuthorLogin", static row => CommitEntity.ReadAuthorLogin(row)),
        ("CommitterName", static row => CommitEntity.ReadCommitterName(row)),
        ("CommitterEmail", static row => CommitEntity.ReadCommitterEmail(row)),
        ("CommitterLogin", static row => CommitEntity.ReadCommitterLogin(row)),
        ("ParentShas", static row => CommitEntity.ReadParentShas(row)),
        ("VerificationReason", static row => CommitEntity.ReadVerificationReason(row))
    );

    internal static readonly PatternColumnBindings<BranchInput> Branch = new(
        ("Name", static row => BranchEntity.ReadName(row.Value)),
        ("CommitSha", static row => BranchEntity.ReadCommitSha(row.Value)),
        ("CommitUrl", static row => BranchEntity.ReadCommitUrl(row.Value)),
        ("RepositoryOwner", static row => row.Owner),
        ("RepositoryName", static row => row.Repository)
    );

    internal static readonly PatternColumnBindings<Release> Release = new(
        ("TagName", static row => ReleaseEntity.ReadTagName(row)),
        ("Name", static row => ReleaseEntity.ReadName(row)),
        ("Body", static row => ReleaseEntity.ReadBody(row)),
        ("Url", static row => ReleaseEntity.ReadUrl(row)),
        ("TargetCommitish", static row => ReleaseEntity.ReadTargetCommitish(row)),
        ("AuthorLogin", static row => ReleaseEntity.ReadAuthorLogin(row)),
        ("TarballUrl", static row => ReleaseEntity.ReadTarballUrl(row)),
        ("ZipballUrl", static row => ReleaseEntity.ReadZipballUrl(row))
    );
}
