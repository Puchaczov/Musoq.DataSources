using System;
using System.Collections.Generic;
using System.Linq;
using LibGit2Sharp;
using Musoq.Plugins.Attributes;

namespace Musoq.DataSources.Git.Entities;

/// <summary>Represents a detached Git patch snapshot.</summary>
public class PatchEntity
{
    private readonly GitNestedSnapshot<PatchSnapshot> _snapshot = new();
    private readonly Func<PatchSnapshot>? _resolve;

    /// <summary>Creates a detached patch snapshot from a LibGit2Sharp patch.</summary>
    /// <param name="patch">The patch to copy.</param>
    /// <param name="repository">The source repository used while copying patch entries.</param>
    public PatchEntity(Patch patch, Repository repository)
    {
        _snapshot.Set(PatchSnapshot.Create(patch, repository));
    }

    internal PatchEntity(int linesAdded, int linesDeleted, string content, PatchEntryChangesEntity[] changes)
    {
        _snapshot.Set(new PatchSnapshot(linesAdded, linesDeleted, content, changes));
    }

    internal PatchEntity(string repositoryPath, string firstCommitSha, string secondCommitSha)
    {
        _resolve = () =>
        {
            using var repository = new Repository(repositoryPath);
            var first = repository.Lookup<Commit>(firstCommitSha) ??
                        throw new InvalidOperationException($"Patch source commit '{firstCommitSha}' is no longer available.");
            var second = repository.Lookup<Commit>(secondCommitSha) ??
                         throw new InvalidOperationException($"Patch target commit '{secondCommitSha}' is no longer available.");
            return PatchSnapshot.Create(repository.Diff.Compare<Patch>(first.Tree, second.Tree), repository);
        };
    }

    /// <summary>Gets the number of added lines in the patch.</summary>
    public int LinesAdded => Snapshot.LinesAdded;

    /// <summary>Gets the number of deleted lines in the patch.</summary>
    public int LinesDeleted => Snapshot.LinesDeleted;

    /// <summary>Gets the unified patch content.</summary>
    public string Content => Snapshot.Content;

    /// <summary>Gets the changed entries contained in the patch.</summary>
    [BindablePropertyAsTable]
    public IEnumerable<PatchEntryChangesEntity> Changes => Snapshot.Changes;

    private PatchSnapshot Snapshot => _snapshot.GetOrCreate(() => _resolve?.Invoke()) ??
                                      throw new InvalidOperationException("Patch snapshot has no source.");

    private sealed record PatchSnapshot(int LinesAdded, int LinesDeleted, string Content, PatchEntryChangesEntity[] Changes)
    {
        public static PatchSnapshot Create(Patch patch, Repository repository) => new(
            patch.LinesAdded,
            patch.LinesDeleted,
            patch.Content,
            patch.Select(change => new PatchEntryChangesEntity(change, repository)).ToArray());
    }
}
