#nullable enable
using System;
using Musoq.DataSources.Common;

namespace Musoq.DataSources.Git;

/// <summary>Reviewed source-row string getters. These never traverse child tables or perform API calls.</summary>
internal static class GitPatternColumns
{
    internal static readonly PatternColumnBindings<Entities.RepositoryEntity> RepositoryEntity = new(
        (nameof(Entities.RepositoryEntity.Path), static row => row.Path),
        (nameof(Entities.RepositoryEntity.WorkingDirectory), static row => row.WorkingDirectory)
    );

    internal static readonly PatternColumnBindings<Entities.CommitEntity> CommitEntity = new(
        (nameof(Entities.CommitEntity.Sha), static row => row.Sha),
        (nameof(Entities.CommitEntity.Message), static row => row.Message),
        (nameof(Entities.CommitEntity.MessageShort), static row => row.MessageShort),
        (nameof(Entities.CommitEntity.Author), static row => row.Author),
        (nameof(Entities.CommitEntity.AuthorEmail), static row => row.AuthorEmail),
        (nameof(Entities.CommitEntity.Committer), static row => row.Committer),
        (nameof(Entities.CommitEntity.CommitterEmail), static row => row.CommitterEmail)
    );

    internal static readonly PatternColumnBindings<Entities.BranchEntity> BranchEntity = new(
        (nameof(Entities.BranchEntity.FriendlyName), static row => row.FriendlyName),
        (nameof(Entities.BranchEntity.CanonicalName), static row => row.CanonicalName),
        (nameof(Entities.BranchEntity.UpstreamBranchCanonicalName), static row => row.UpstreamBranchCanonicalName),
        (nameof(Entities.BranchEntity.RemoteName), static row => row.RemoteName)
    );

    internal static readonly PatternColumnBindings<Entities.TagEntity> TagEntity = new(
        (nameof(Entities.TagEntity.FriendlyName), static row => row.FriendlyName),
        (nameof(Entities.TagEntity.CanonicalName), static row => row.CanonicalName),
        (nameof(Entities.TagEntity.TargetSha), static row => row.TargetSha),
        (nameof(Entities.TagEntity.Message), static row => row.Message)
    );

    internal static readonly PatternColumnBindings<Entities.StashEntity> StashEntity = new(
        (nameof(Entities.StashEntity.Selector), static row => row.Selector),
        (nameof(Entities.StashEntity.Sha), static row => row.Sha),
        (nameof(Entities.StashEntity.Message), static row => row.Message)
    );

    internal static readonly PatternColumnBindings<Entities.RemoteTagEntity> RemoteTagEntity = new(
        (nameof(Entities.RemoteTagEntity.RemoteName), static row => row.RemoteName),
        (nameof(Entities.RemoteTagEntity.RemoteUrl), static row => row.RemoteUrl),
        (nameof(Entities.RemoteTagEntity.FriendlyName), static row => row.FriendlyName),
        (nameof(Entities.RemoteTagEntity.CanonicalName), static row => row.CanonicalName),
        (nameof(Entities.RemoteTagEntity.ObjectSha), static row => row.ObjectSha),
        (nameof(Entities.RemoteTagEntity.PeeledSha), static row => row.PeeledSha)
    );

    internal static readonly PatternColumnBindings<Entities.FileHistoryEntity> FileHistoryEntity = new(
        (nameof(Entities.FileHistoryEntity.CommitSha), static row => row.CommitSha),
        (nameof(Entities.FileHistoryEntity.Author), static row => row.Author),
        (nameof(Entities.FileHistoryEntity.AuthorEmail), static row => row.AuthorEmail),
        (nameof(Entities.FileHistoryEntity.FilePath), static row => row.FilePath),
        (nameof(Entities.FileHistoryEntity.ChangeType), static row => row.ChangeType),
        (nameof(Entities.FileHistoryEntity.OldPath), static row => row.OldPath)
    );

    internal static readonly PatternColumnBindings<Entities.StatusEntity> StatusEntity = new(
        (nameof(Entities.StatusEntity.FilePath), static row => row.FilePath),
        (nameof(Entities.StatusEntity.State), static row => row.State),
        (nameof(Entities.StatusEntity.IndexStatus), static row => row.IndexStatus),
        (nameof(Entities.StatusEntity.WorkDirStatus), static row => row.WorkDirStatus)
    );

    internal static readonly PatternColumnBindings<Entities.RemoteEntity> RemoteEntity = new(
        (nameof(Entities.RemoteEntity.Name), static row => row.Name),
        (nameof(Entities.RemoteEntity.Url), static row => row.Url),
        (nameof(Entities.RemoteEntity.PushUrl), static row => row.PushUrl)
    );

    internal static readonly PatternColumnBindings<Entities.BlameHunkEntity> BlameHunkEntity = new(
        (nameof(Entities.BlameHunkEntity.CommitSha), static row => row.CommitSha),
        (nameof(Entities.BlameHunkEntity.Author), static row => row.Author),
        (nameof(Entities.BlameHunkEntity.AuthorEmail), static row => row.AuthorEmail),
        (nameof(Entities.BlameHunkEntity.Committer), static row => row.Committer),
        (nameof(Entities.BlameHunkEntity.CommitterEmail), static row => row.CommitterEmail),
        (nameof(Entities.BlameHunkEntity.Summary), static row => row.Summary),
        (nameof(Entities.BlameHunkEntity.OriginalFilePath), static row => row.OriginalFilePath)
    );

    internal static IPatternColumnBindings? Get(string name) => name.ToLowerInvariant() switch
    {
        "repository" => RepositoryEntity,
        "commits" => CommitEntity,
        "branches" => BranchEntity,
        "tags" => TagEntity,
        "stashes" => StashEntity,
        "remotetags" => RemoteTagEntity,
        "filehistory" => FileHistoryEntity,
        "status" => StatusEntity,
        "remotes" => RemoteEntity,
        "blame" => BlameHunkEntity,
        _ => null
    };
}
