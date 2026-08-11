using System;
using LibGit2Sharp;
using Musoq.DataSources.Git.Entities;

namespace Musoq.DataSources.Git;

/// <summary>
/// Creates detached row snapshots shaped to the runtime-v2 physical projection. A source must still provide predicate
/// dependencies, but it does not read unrelated LibGit2Sharp properties merely because the public row type exposes
/// them.
/// </summary>
internal static class GitEntitySnapshots
{
    public static CommitEntity Commit(Commit commit, Repository repository, GitProjection projection)
    {
        var all = projection.Includes(nameof(CommitEntity.Self));
        // SHA plus repository identity are the compact capability token for public nested APIs and library methods.
        // The runtime does not always expose injected/nested dependencies in RequiredColumns.
        var needsSha = true;
        var needsPath = true;

        return new CommitEntity(
            needsPath ? repository.Info.Path : null,
            needsSha ? commit.Sha : null,
            Includes(projection, all, nameof(CommitEntity.Message)) ? commit.Message : null,
            Includes(projection, all, nameof(CommitEntity.MessageShort)) ? commit.MessageShort : null,
            Includes(projection, all, nameof(CommitEntity.Author)) ? commit.Author.Name : null,
            Includes(projection, all, nameof(CommitEntity.AuthorEmail)) ? commit.Author.Email : null,
            Includes(projection, all, nameof(CommitEntity.Committer)) ? commit.Committer.Name : null,
            Includes(projection, all, nameof(CommitEntity.CommitterEmail)) ? commit.Committer.Email : null,
            Includes(projection, all, nameof(CommitEntity.CommittedWhen)) ? commit.Committer.When : default);
    }

    public static CommitEntity Commit(GitCommitRecord commit, GitProjection projection)
    {
        var all = projection.Includes(nameof(CommitEntity.Self));
        return new CommitEntity(
            commit.RepositoryPath,
            commit.Sha,
            Includes(projection, all, nameof(CommitEntity.Message)) ? commit.Message : null,
            Includes(projection, all, nameof(CommitEntity.MessageShort)) ? commit.MessageShort : null,
            Includes(projection, all, nameof(CommitEntity.Author)) ? commit.Author : null,
            Includes(projection, all, nameof(CommitEntity.AuthorEmail)) ? commit.AuthorEmail : null,
            Includes(projection, all, nameof(CommitEntity.Committer)) ? commit.Committer : null,
            Includes(projection, all, nameof(CommitEntity.CommitterEmail)) ? commit.CommitterEmail : null,
            Includes(projection, all, nameof(CommitEntity.CommittedWhen)) ? commit.CommittedWhen : default);
    }

    public static BranchEntity Branch(Branch branch, Repository repository, GitProjection projection)
    {
        var all = projection.Includes(nameof(BranchEntity.Self));
        // Keep the small branch identity needed by nested and library calls even when the compiler only reports the
        // downstream leaf (for example Tip.Sha) rather than the intermediate BranchEntity column.
        var needsCanonical = true;
        var needsTracking = true;
        var needsTip = all || projection.Includes(nameof(BranchEntity.Tip));

        return new BranchEntity(
            repository.Info.Path,
            Includes(projection, all, nameof(BranchEntity.FriendlyName)) ? branch.FriendlyName : string.Empty,
            needsCanonical ? branch.CanonicalName : string.Empty,
            Includes(projection, all, nameof(BranchEntity.IsRemote)) && branch.IsRemote,
            Includes(projection, all, nameof(BranchEntity.IsTracking)) && branch.IsTracking,
            Includes(projection, all, nameof(BranchEntity.IsCurrentRepositoryHead)) && branch.IsCurrentRepositoryHead,
            all || projection.Includes(nameof(BranchEntity.TrackedBranch)) ? branch.TrackedBranch?.CanonicalName : null,
            needsTracking ? branch.TrackingDetails?.AheadBy : null,
            needsTracking ? branch.TrackingDetails?.BehindBy : null,
            needsTip ? branch.Tip?.Sha : null,
            Includes(projection, all, nameof(BranchEntity.UpstreamBranchCanonicalName)) ? branch.UpstreamBranchCanonicalName : null,
            Includes(projection, all, nameof(BranchEntity.RemoteName)) ? branch.RemoteName : null);
    }

    public static BranchEntity Branch(GitBranchRecord branch, GitProjection projection)
    {
        var all = projection.Includes(nameof(BranchEntity.Self));
        return new BranchEntity(
            branch.RepositoryPath,
            Includes(projection, all, nameof(BranchEntity.FriendlyName)) ? branch.FriendlyName : string.Empty,
            branch.CanonicalName,
            Includes(projection, all, nameof(BranchEntity.IsRemote)) && branch.IsRemote,
            Includes(projection, all, nameof(BranchEntity.IsTracking)) && branch.IsTracking,
            Includes(projection, all, nameof(BranchEntity.IsCurrentRepositoryHead)) && branch.IsCurrentRepositoryHead,
            all || projection.Includes(nameof(BranchEntity.TrackedBranch)) ? branch.TrackedBranchCanonicalName : null,
            branch.AheadBy,
            branch.BehindBy,
            branch.TipSha,
            Includes(projection, all, nameof(BranchEntity.UpstreamBranchCanonicalName)) ? branch.UpstreamBranchCanonicalName : null,
            Includes(projection, all, nameof(BranchEntity.RemoteName)) ? branch.RemoteName : null);
    }

    public static TagEntity Tag(Tag tag, Repository repository, GitProjection projection)
    {
        var all = false;
        // A tag's target and annotation are compact references and may be required through an unreported nested leaf.
        var needsAnnotation = true;
        var needsCommit = true;
        var annotation = needsAnnotation && tag.Annotation is { } tagAnnotation
            ? new AnnotationEntity(tagAnnotation, repository)
            : null;
        return new TagEntity(
            repository.Info.Path,
            Includes(projection, all, nameof(TagEntity.FriendlyName)) ? tag.FriendlyName : null,
            Includes(projection, all, nameof(TagEntity.CanonicalName)) ? tag.CanonicalName : null,
            Includes(projection, all, nameof(TagEntity.Message)) ? tag.Annotation?.Message : null,
            Includes(projection, all, nameof(TagEntity.IsAnnotated)) && tag.IsAnnotated,
            annotation,
            needsCommit ? (tag.Target as Commit)?.Sha : null);
    }

    public static TagEntity Tag(GitTagRecord tag, GitProjection projection)
    {
        return new TagEntity(
            tag.RepositoryPath,
            projection.Includes(nameof(TagEntity.FriendlyName)) ? tag.FriendlyName : null,
            projection.Includes(nameof(TagEntity.CanonicalName)) ? tag.CanonicalName : null,
            projection.Includes(nameof(TagEntity.Message)) ? tag.Message : null,
            projection.Includes(nameof(TagEntity.IsAnnotated)) && tag.IsAnnotated,
            tag.Annotation,
            tag.CommitSha);
    }

    public static RemoteEntity Remote(Remote remote, GitProjection projection)
    {
        return new RemoteEntity(
            projection.Includes(nameof(RemoteEntity.Name)) ? remote.Name : string.Empty,
            projection.Includes(nameof(RemoteEntity.Url)) ? remote.Url : string.Empty,
            projection.Includes(nameof(RemoteEntity.PushUrl)) ? remote.PushUrl : null);
    }

    public static RemoteEntity Remote(GitRemoteRecord remote, GitProjection projection)
    {
        return new RemoteEntity(
            projection.Includes(nameof(RemoteEntity.Name)) ? remote.Name : string.Empty,
            projection.Includes(nameof(RemoteEntity.Url)) ? remote.Url : string.Empty,
            projection.Includes(nameof(RemoteEntity.PushUrl)) ? remote.PushUrl : null);
    }

    public static StatusEntity Status(StatusEntry entry, GitProjection projection)
    {
        var state = projection.Includes(nameof(StatusEntity.State)) || projection.Includes(nameof(StatusEntity.IndexStatus)) ||
                    projection.Includes(nameof(StatusEntity.WorkDirStatus));
        if (!state)
            return new StatusEntity(
                projection.Includes(nameof(StatusEntity.FilePath)) ? entry.FilePath : string.Empty,
                string.Empty,
                string.Empty,
                string.Empty);

        return new StatusEntity(
            projection.Includes(nameof(StatusEntity.FilePath)) ? entry.FilePath : string.Empty,
            entry.State.ToString(),
            entry.State.HasFlag(FileStatus.NewInIndex) || entry.State.HasFlag(FileStatus.ModifiedInIndex) ||
            entry.State.HasFlag(FileStatus.DeletedFromIndex) || entry.State.HasFlag(FileStatus.RenamedInIndex) ||
            entry.State.HasFlag(FileStatus.TypeChangeInIndex) ? "Staged" : "NotStaged",
            entry.State.HasFlag(FileStatus.NewInWorkdir) || entry.State.HasFlag(FileStatus.ModifiedInWorkdir) ||
            entry.State.HasFlag(FileStatus.DeletedFromWorkdir) || entry.State.HasFlag(FileStatus.RenamedInWorkdir) ||
            entry.State.HasFlag(FileStatus.TypeChangeInWorkdir) ? "Modified" : "Unmodified");
    }

    public static StatusEntity Status(GitStatusRecord entry, GitProjection projection)
    {
        var state = projection.Includes(nameof(StatusEntity.State)) || projection.Includes(nameof(StatusEntity.IndexStatus)) ||
                    projection.Includes(nameof(StatusEntity.WorkDirStatus));
        if (!state)
            return new StatusEntity(
                projection.Includes(nameof(StatusEntity.FilePath)) ? entry.FilePath : string.Empty,
                string.Empty,
                string.Empty,
                string.Empty);

        return new StatusEntity(
            projection.Includes(nameof(StatusEntity.FilePath)) ? entry.FilePath : string.Empty,
            entry.State.ToString(),
            entry.State.HasFlag(FileStatus.NewInIndex) || entry.State.HasFlag(FileStatus.ModifiedInIndex) ||
            entry.State.HasFlag(FileStatus.DeletedFromIndex) || entry.State.HasFlag(FileStatus.RenamedInIndex) ||
            entry.State.HasFlag(FileStatus.TypeChangeInIndex) ? "Staged" : "NotStaged",
            entry.State.HasFlag(FileStatus.NewInWorkdir) || entry.State.HasFlag(FileStatus.ModifiedInWorkdir) ||
            entry.State.HasFlag(FileStatus.DeletedFromWorkdir) || entry.State.HasFlag(FileStatus.RenamedInWorkdir) ||
            entry.State.HasFlag(FileStatus.TypeChangeInWorkdir) ? "Modified" : "Unmodified");
    }

    public static RepositoryEntity Repository(Repository repository, GitProjection projection)
    {
        // Repository rows are also injected into library methods; always preserve their compact identity and info
        // snapshot while keeping the potentially large nested enumerables lazy.
        var needsPath = true;
        var information = new RepositoryInformationEntity(repository.Info, repository);
        return new RepositoryEntity(
            needsPath ? repository.Info.Path : string.Empty,
            repository.Info.WorkingDirectory,
            information);
    }

    public static FileHistoryEntity FileHistory(
        string commitSha,
        string author,
        string authorEmail,
        DateTimeOffset committedWhen,
        string filePath,
        string changeType,
        string? oldPath,
        GitProjection projection)
    {
        return new FileHistoryEntity(
            projection.Includes(nameof(FileHistoryEntity.CommitSha)) ? commitSha : null!,
            projection.Includes(nameof(FileHistoryEntity.Author)) ? author : null!,
            projection.Includes(nameof(FileHistoryEntity.AuthorEmail)) ? authorEmail : null!,
            projection.Includes(nameof(FileHistoryEntity.CommittedWhen)) ? committedWhen : default,
            projection.Includes(nameof(FileHistoryEntity.FilePath)) ? filePath : null!,
            projection.Includes(nameof(FileHistoryEntity.ChangeType)) ? changeType : null!,
            projection.Includes(nameof(FileHistoryEntity.OldPath)) ? oldPath : null);
    }

    private static bool Includes(GitProjection projection, bool all, string column) => all || projection.Includes(column);
}
