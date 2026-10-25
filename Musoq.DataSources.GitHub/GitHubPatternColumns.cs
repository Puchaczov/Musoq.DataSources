#nullable enable
using System;
using Musoq.DataSources.Common;

namespace Musoq.DataSources.GitHub;

/// <summary>Reviewed source-row string getters. These never traverse child tables or perform API calls.</summary>
internal static class GitHubPatternColumns
{
    internal static readonly PatternColumnBindings<Entities.RepositoryEntity> RepositoryEntity = new(
        (nameof(Entities.RepositoryEntity.Name), static row => row.Name),
        (nameof(Entities.RepositoryEntity.FullName), static row => row.FullName),
        (nameof(Entities.RepositoryEntity.Description), static row => row.Description),
        (nameof(Entities.RepositoryEntity.Url), static row => row.Url),
        (nameof(Entities.RepositoryEntity.CloneUrl), static row => row.CloneUrl),
        (nameof(Entities.RepositoryEntity.SshUrl), static row => row.SshUrl),
        (nameof(Entities.RepositoryEntity.DefaultBranch), static row => row.DefaultBranch),
        (nameof(Entities.RepositoryEntity.Language), static row => row.Language),
        (nameof(Entities.RepositoryEntity.OwnerLogin), static row => row.OwnerLogin),
        (nameof(Entities.RepositoryEntity.License), static row => row.License),
        (nameof(Entities.RepositoryEntity.Visibility), static row => row.Visibility)
    );

    internal static readonly PatternColumnBindings<Entities.IssueEntity> IssueEntity = new(
        (nameof(Entities.IssueEntity.Title), static row => row.Title),
        (nameof(Entities.IssueEntity.Body), static row => row.Body),
        (nameof(Entities.IssueEntity.State), static row => row.State),
        (nameof(Entities.IssueEntity.Url), static row => row.Url),
        (nameof(Entities.IssueEntity.AuthorLogin), static row => row.AuthorLogin),
        (nameof(Entities.IssueEntity.AssigneeLogin), static row => row.AssigneeLogin),
        (nameof(Entities.IssueEntity.Assignees), static row => row.Assignees),
        (nameof(Entities.IssueEntity.Labels), static row => row.Labels),
        (nameof(Entities.IssueEntity.MilestoneTitle), static row => row.MilestoneTitle),
        (nameof(Entities.IssueEntity.ClosedByLogin), static row => row.ClosedByLogin),
        (nameof(Entities.IssueEntity.ActiveLockReason), static row => row.ActiveLockReason),
        (nameof(Entities.IssueEntity.RepositoryUrl), static row => row.RepositoryUrl),
        (nameof(Entities.IssueEntity.StateReason), static row => row.StateReason)
    );

    internal static readonly PatternColumnBindings<Entities.PullRequestEntity> PullRequestEntity = new(
        (nameof(Entities.PullRequestEntity.Title), static row => row.Title),
        (nameof(Entities.PullRequestEntity.Body), static row => row.Body),
        (nameof(Entities.PullRequestEntity.State), static row => row.State),
        (nameof(Entities.PullRequestEntity.Url), static row => row.Url),
        (nameof(Entities.PullRequestEntity.AuthorLogin), static row => row.AuthorLogin),
        (nameof(Entities.PullRequestEntity.AssigneeLogin), static row => row.AssigneeLogin),
        (nameof(Entities.PullRequestEntity.Assignees), static row => row.Assignees),
        (nameof(Entities.PullRequestEntity.Labels), static row => row.Labels),
        (nameof(Entities.PullRequestEntity.MilestoneTitle), static row => row.MilestoneTitle),
        (nameof(Entities.PullRequestEntity.HeadRef), static row => row.HeadRef),
        (nameof(Entities.PullRequestEntity.HeadSha), static row => row.HeadSha),
        (nameof(Entities.PullRequestEntity.HeadRepository), static row => row.HeadRepository),
        (nameof(Entities.PullRequestEntity.BaseRef), static row => row.BaseRef),
        (nameof(Entities.PullRequestEntity.BaseSha), static row => row.BaseSha),
        (nameof(Entities.PullRequestEntity.BaseRepository), static row => row.BaseRepository),
        (nameof(Entities.PullRequestEntity.MergeableState), static row => row.MergeableState),
        (nameof(Entities.PullRequestEntity.MergedByLogin), static row => row.MergedByLogin),
        (nameof(Entities.PullRequestEntity.MergeCommitSha), static row => row.MergeCommitSha),
        (nameof(Entities.PullRequestEntity.ActiveLockReason), static row => row.ActiveLockReason)
    );

    internal static readonly PatternColumnBindings<Entities.CommitEntity> CommitEntity = new(
        (nameof(Entities.CommitEntity.Sha), static row => row.Sha),
        (nameof(Entities.CommitEntity.ShortSha), static row => row.ShortSha),
        (nameof(Entities.CommitEntity.Message), static row => row.Message),
        (nameof(Entities.CommitEntity.Url), static row => row.Url),
        (nameof(Entities.CommitEntity.AuthorName), static row => row.AuthorName),
        (nameof(Entities.CommitEntity.AuthorEmail), static row => row.AuthorEmail),
        (nameof(Entities.CommitEntity.AuthorLogin), static row => row.AuthorLogin),
        (nameof(Entities.CommitEntity.CommitterName), static row => row.CommitterName),
        (nameof(Entities.CommitEntity.CommitterEmail), static row => row.CommitterEmail),
        (nameof(Entities.CommitEntity.CommitterLogin), static row => row.CommitterLogin),
        (nameof(Entities.CommitEntity.ParentShas), static row => row.ParentShas),
        (nameof(Entities.CommitEntity.VerificationReason), static row => row.VerificationReason)
    );

    internal static readonly PatternColumnBindings<Entities.BranchEntity> BranchEntity = new(
        (nameof(Entities.BranchEntity.Name), static row => row.Name),
        (nameof(Entities.BranchEntity.CommitSha), static row => row.CommitSha),
        (nameof(Entities.BranchEntity.CommitUrl), static row => row.CommitUrl),
        (nameof(Entities.BranchEntity.RepositoryOwner), static row => row.RepositoryOwner),
        (nameof(Entities.BranchEntity.RepositoryName), static row => row.RepositoryName)
    );

    internal static readonly PatternColumnBindings<Entities.ReleaseEntity> ReleaseEntity = new(
        (nameof(Entities.ReleaseEntity.TagName), static row => row.TagName),
        (nameof(Entities.ReleaseEntity.Name), static row => row.Name),
        (nameof(Entities.ReleaseEntity.Body), static row => row.Body),
        (nameof(Entities.ReleaseEntity.Url), static row => row.Url),
        (nameof(Entities.ReleaseEntity.TargetCommitish), static row => row.TargetCommitish),
        (nameof(Entities.ReleaseEntity.AuthorLogin), static row => row.AuthorLogin),
        (nameof(Entities.ReleaseEntity.TarballUrl), static row => row.TarballUrl),
        (nameof(Entities.ReleaseEntity.ZipballUrl), static row => row.ZipballUrl)
    );

    internal static IPatternColumnBindings? Get(string name) => name.ToLowerInvariant() switch
    {
        "repositories" => RepositoryEntity,
        "issues" => IssueEntity,
        "pullrequests" => PullRequestEntity,
        "commits" => CommitEntity,
        "branchcommits" => CommitEntity,
        "branches" => BranchEntity,
        "releases" => ReleaseEntity,
        _ => null
    };
}
