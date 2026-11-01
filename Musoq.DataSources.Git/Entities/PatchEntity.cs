using System;
using System.Collections.Generic;
using System.Linq;
using LibGit2Sharp;
using Musoq.Plugins.Attributes;

namespace Musoq.DataSources.Git.Entities;

/// <summary>Represents a detached Git patch with separately deferred statistics and text.</summary>
public class PatchEntity
{
    private readonly GitNestedSnapshot<PatchSnapshot> _snapshot = new();
    private readonly GitNestedSnapshot<PatchStatistics> _statistics = new();
    private readonly Func<PatchSnapshot>? _resolve;
    private readonly Func<PatchStatistics>? _resolveStatistics;

    /// <summary>Copies a detached patch from a live LibGit2Sharp patch.</summary>
    /// <param name="patch">The patch to copy.</param>
    /// <param name="repository">The repository used while copying entries.</param>
    public PatchEntity(Patch patch, Repository repository) => Set(PatchSnapshot.Create(patch, repository));

    internal PatchEntity(string repositoryPath, string firstCommitSha, string secondCommitSha)
    {
        _resolve = () => ReadNative(repositoryPath, firstCommitSha, secondCommitSha, PatchSnapshot.Create);
        _resolveStatistics = () => ReadNative(repositoryPath, firstCommitSha, secondCommitSha,
            static (patch, _) => new PatchStatistics(patch.LinesAdded, patch.LinesDeleted));
    }

    internal PatchEntity(GitRepositoryContext context, string firstCommitSha, string secondCommitSha)
    {
        _resolve = () =>
        {
            var snapshot = context.PatchData(firstCommitSha, secondCommitSha);
            return new PatchSnapshot(snapshot.Added, snapshot.Deleted, snapshot.Content, snapshot.Changes);
        };
        _resolveStatistics = () =>
        {
            var counts = context.PatchStatistics(firstCommitSha, secondCommitSha);
            return new PatchStatistics(counts.Added, counts.Deleted);
        };
    }

    /// <summary>Gets the added-line count without loading unified text.</summary>
    public int LinesAdded => Statistics.Added;
    /// <summary>Gets the deleted-line count without loading unified text.</summary>
    public int LinesDeleted => Statistics.Deleted;
    /// <summary>Gets the unified patch content.</summary>
    public string Content => Snapshot.Content;
    /// <summary>Gets detached changed entries including their textual patches.</summary>
    [BindablePropertyAsTable]
    public IEnumerable<PatchEntryChangesEntity> Changes => Snapshot.Changes;

    private PatchStatistics Statistics => _statistics.GetOrCreate(() => _resolveStatistics?.Invoke() ??
        new PatchStatistics(Snapshot.LinesAdded, Snapshot.LinesDeleted))!;

    private PatchSnapshot Snapshot
    {
        get
        {
            var value = _snapshot.GetOrCreate(() => _resolve?.Invoke()) ??
                throw new InvalidOperationException("Patch snapshot has no source.");
            _statistics.GetOrCreate(() => new PatchStatistics(value.LinesAdded, value.LinesDeleted));
            return value;
        }
    }

    private void Set(PatchSnapshot snapshot)
    {
        _snapshot.Set(snapshot);
        _statistics.Set(new PatchStatistics(snapshot.LinesAdded, snapshot.LinesDeleted));
    }

    private static TResult ReadNative<TResult>(string path, string firstSha, string secondSha,
        Func<Patch, Repository, TResult> copy)
    {
        using var repository = new Repository(path);
        var first = repository.Lookup<Commit>(firstSha) ??
            throw new InvalidOperationException($"Patch source commit '{firstSha}' is no longer available.");
        var second = repository.Lookup<Commit>(secondSha) ??
            throw new InvalidOperationException($"Patch target commit '{secondSha}' is no longer available.");
        using var patch = repository.Diff.Compare<Patch>(first.Tree, second.Tree);
        return copy(patch, repository);
    }

    private sealed record PatchStatistics(int Added, int Deleted);
    private sealed record PatchSnapshot(int LinesAdded, int LinesDeleted, string Content, PatchEntryChangesEntity[] Changes)
    {
        public static PatchSnapshot Create(Patch patch, Repository repository) => new(patch.LinesAdded,
            patch.LinesDeleted, patch.Content,
            patch.Select(change => new PatchEntryChangesEntity(change, repository)).ToArray());
    }
}
