using System;
using System.Collections.Generic;
using LibGit2Sharp;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Git.Entities;

/// <summary>Represents a detached Git stash snapshot.</summary>
public class StashEntity
{
    /// <summary>Maps SQL-visible column names to row indexes.</summary>
    public static readonly IReadOnlyDictionary<string, int> NameToIndexMap;

    /// <summary>Maps row indexes to property accessors.</summary>
    public static readonly IReadOnlyDictionary<int, Func<StashEntity, object?>> IndexToObjectAccessMap;

    /// <summary>Describes the columns exposed by a stash row.</summary>
    public static readonly ISchemaColumn[] Columns =
    [
        new SchemaColumn(nameof(Selector), 0, typeof(string)),
        new SchemaColumn(nameof(Sha), 1, typeof(string)),
        new SchemaColumn(nameof(Message), 2, typeof(string)),
        new SchemaColumn(nameof(Index), 3, typeof(CommitEntity)),
        new SchemaColumn(nameof(WorkTree), 4, typeof(CommitEntity)),
        new SchemaColumn(nameof(UntrackedFiles), 5, typeof(CommitEntity))
    ];

    private readonly string _repositoryPath;
    private readonly string _selector;
    private readonly string _sha;
    private readonly string _message;
    private readonly string? _indexSha;
    private readonly string? _workTreeSha;
    private readonly string? _untrackedFilesSha;
    private readonly GitNestedSnapshot<CommitEntity> _index = new();
    private readonly GitNestedSnapshot<CommitEntity> _workTree = new();
    private readonly GitNestedSnapshot<CommitEntity> _untrackedFiles = new();

    static StashEntity()
    {
        NameToIndexMap = new Dictionary<string, int>
        {
            { nameof(Selector), 0 }, { nameof(Sha), 1 }, { nameof(Message), 2 },
            { nameof(Index), 3 }, { nameof(WorkTree), 4 }, { nameof(UntrackedFiles), 5 }
        };
        IndexToObjectAccessMap = new Dictionary<int, Func<StashEntity, object?>>
        {
            { 0, entity => entity.Selector }, { 1, entity => entity.Sha }, { 2, entity => entity.Message },
            { 3, entity => entity.Index }, { 4, entity => entity.WorkTree }, { 5, entity => entity.UntrackedFiles }
        };
    }

    /// <summary>Creates a detached stash snapshot from a LibGit2Sharp stash.</summary>
    /// <param name="stash">The stash to copy.</param>
    /// <param name="repository">The source repository; it is used only to capture its path and commit identifiers.</param>
    public StashEntity(Stash stash, Repository repository)
        : this(repository.Info.Path, string.Empty, stash.WorkTree?.Sha ?? string.Empty, stash.Message,
            stash.Index?.Sha, stash.WorkTree?.Sha, stash.Untracked?.Sha)
    {
    }

    internal StashEntity(
        string repositoryPath,
        string selector,
        string sha,
        string message,
        string? indexSha,
        string? workTreeSha,
        string? untrackedFilesSha)
    {
        _repositoryPath = repositoryPath;
        _selector = selector;
        _sha = sha;
        _message = message;
        _indexSha = indexSha;
        _workTreeSha = workTreeSha;
        _untrackedFilesSha = untrackedFilesSha;
    }

    internal StashEntity(
        string repositoryPath,
        string message,
        string? indexSha,
        string? workTreeSha,
        string? untrackedFilesSha)
        : this(repositoryPath, string.Empty, workTreeSha ?? string.Empty, message, indexSha, workTreeSha,
            untrackedFilesSha)
    {
    }

    /// <summary>Gets the reflog selector, such as <c>stash@{0}</c>.</summary>
    public string Selector => _selector;

    /// <summary>Gets the stash work-tree commit SHA.</summary>
    public string Sha => _sha;

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
