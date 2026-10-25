#nullable enable
using LibGit2Sharp;
using Musoq.DataSources.Common;

namespace Musoq.DataSources.Git;

internal static class GitNativePatterns
{
    internal static readonly PatternColumnBindings<GitCommitRecord> Commits = new(
        ("Sha", row => row.Sha), ("Message", row => row.Message), ("MessageShort", row => row.MessageShort),
        ("Author", row => row.Author), ("AuthorEmail", row => row.AuthorEmail),
        ("Committer", row => row.Committer), ("CommitterEmail", row => row.CommitterEmail));
    internal static readonly PatternColumnBindings<Branch> Branches = new(
        ("FriendlyName", row => row.FriendlyName), ("CanonicalName", row => row.CanonicalName),
        ("UpstreamBranchCanonicalName", row => row.UpstreamBranchCanonicalName), ("RemoteName", row => row.RemoteName));
    internal static readonly PatternColumnBindings<GitBranchRecord> BranchRecords = new(
        ("FriendlyName", row => row.FriendlyName), ("CanonicalName", row => row.CanonicalName),
        ("UpstreamBranchCanonicalName", row => row.UpstreamBranchCanonicalName), ("RemoteName", row => row.RemoteName));
    internal static readonly PatternColumnBindings<Tag> Tags = new(
        ("FriendlyName", row => row.FriendlyName), ("CanonicalName", row => row.CanonicalName),
        ("TargetSha", row => (row.PeeledTarget ?? row.Target)?.Sha), ("Message", row => row.Annotation?.Message));
    internal static readonly PatternColumnBindings<GitTagRecord> TagRecords = new(
        ("FriendlyName", row => row.FriendlyName), ("CanonicalName", row => row.CanonicalName),
        ("TargetSha", row => row.TargetSha), ("Message", row => row.Message));
    internal static readonly PatternColumnBindings<GitStashRecord> Stashes = new(
        ("Selector", row => row.Selector), ("Sha", row => row.Sha), ("Message", row => row.Message));
    internal static readonly PatternColumnBindings<Remote> Remotes = new(
        ("Name", row => row.Name), ("Url", row => row.Url), ("PushUrl", row => row.PushUrl));
    internal static readonly PatternColumnBindings<GitRemoteRecord> RemoteRecords = new(
        ("Name", row => row.Name), ("Url", row => row.Url), ("PushUrl", row => row.PushUrl));
    internal static readonly PatternColumnBindings<GitRemoteTagRecord> RemoteTags = new(
        ("RemoteName", row => row.RemoteName), ("RemoteUrl", row => row.RemoteUrl), ("FriendlyName", row => row.FriendlyName),
        ("CanonicalName", row => row.CanonicalName), ("ObjectSha", row => row.ObjectSha), ("PeeledSha", row => row.PeeledSha));
    internal static readonly PatternColumnBindings<Repository> Repositories = new(
        ("Path", row => row.Info.Path), ("WorkingDirectory", row => row.Info.WorkingDirectory));
    internal static readonly PatternColumnBindings<GitStatusRecord> Status = new(
        ("FilePath", row => row.FilePath), ("State", row => row.State.ToString()),
        ("IndexStatus", row => GitEntitySnapshots.IndexStatus(row.State)),
        ("WorkDirStatus", row => GitEntitySnapshots.WorkDirStatus(row.State)));
    internal readonly record struct BlameInput(BlameHunk Hunk, string FilePath);
    internal static readonly PatternColumnBindings<BlameInput> Blame = new(
        ("CommitSha", row => row.Hunk.FinalCommit.Sha), ("Author", row => row.Hunk.FinalCommit.Author.Name),
        ("AuthorEmail", row => row.Hunk.FinalCommit.Author.Email), ("Committer", row => row.Hunk.FinalCommit.Committer.Name),
        ("CommitterEmail", row => row.Hunk.FinalCommit.Committer.Email), ("Summary", row => row.Hunk.FinalCommit.MessageShort),
        ("OriginalFilePath", row => row.Hunk.InitialPath != row.FilePath ? row.Hunk.InitialPath : null));
}
