using LibGit2Sharp;

namespace Musoq.DataSources.Git.Entities;

/// <summary>Represents a detached Git stash snapshot.</summary>
public class StashEntity
{
    private readonly string _repositoryPath;
    private readonly string _message;
    private readonly string? _indexSha;
    private readonly string? _workTreeSha;
    private readonly string? _untrackedFilesSha;
    private readonly GitNestedSnapshot<CommitEntity> _index = new();
    private readonly GitNestedSnapshot<CommitEntity> _workTree = new();
    private readonly GitNestedSnapshot<CommitEntity> _untrackedFiles = new();

    /// <summary>Creates a detached stash snapshot from a LibGit2Sharp stash.</summary>
    /// <param name="stash">The stash to copy.</param>
    /// <param name="repository">The source repository; it is used only to capture its path and commit identifiers.</param>
    public StashEntity(Stash stash, Repository repository)
        : this(repository.Info.Path, stash.Message, stash.Index?.Sha, stash.WorkTree?.Sha, stash.Untracked?.Sha)
    {
    }

    internal StashEntity(
        string repositoryPath,
        string message,
        string? indexSha,
        string? workTreeSha,
        string? untrackedFilesSha)
    {
        _repositoryPath = repositoryPath;
        _message = message;
        _indexSha = indexSha;
        _workTreeSha = workTreeSha;
        _untrackedFilesSha = untrackedFilesSha;
    }

    /// <summary>Gets the stash message.</summary>
    public string Message => _message;

    /// <summary>Gets the index commit, or <see langword="null"/> when the stash has no index commit.</summary>
    /// <remarks>The commit is resolved lazily in a short-lived repository scope and then cached as a detached snapshot.</remarks>
    public CommitEntity? Index => _index.GetOrCreate(() => ResolveCommit(_indexSha));

    /// <summary>Gets the work-tree commit, or <see langword="null"/> when unavailable.</summary>
    /// <remarks>The commit is resolved lazily in a short-lived repository scope and then cached as a detached snapshot.</remarks>
    public CommitEntity? WorkTree => _workTree.GetOrCreate(() => ResolveCommit(_workTreeSha));

    /// <summary>Gets the untracked-files commit, or <see langword="null"/> when unavailable.</summary>
    /// <remarks>The commit is resolved lazily in a short-lived repository scope and then cached as a detached snapshot.</remarks>
    public CommitEntity? UntrackedFiles => _untrackedFiles.GetOrCreate(() => ResolveCommit(_untrackedFilesSha));

    private CommitEntity? ResolveCommit(string? sha)
    {
        if (string.IsNullOrWhiteSpace(sha))
            return null;

        using var repository = new Repository(_repositoryPath);
        var commit = repository.Lookup<Commit>(sha);
        return commit is null ? null : new CommitEntity(commit, repository);
    }
}
