using System;
using System.IO;
using LibGit2Sharp;

namespace Musoq.DataSources.Git.Entities;

/// <summary>Represents a detached tree difference. Blob content is loaded on demand in a short-lived scope.</summary>
public class DifferenceEntity
{
    private readonly string _repositoryPath;
    private readonly string _path;
    private readonly bool _exists;
    private readonly string _changeKind;
    private readonly string? _oldPath;
    private readonly string _oldMode;
    private readonly string _newMode;
    private readonly string? _oldSha;
    private readonly string? _newSha;
    private readonly bool _isDeleted;
    private readonly GitNestedSnapshot<string> _oldContent = new();
    private readonly GitNestedSnapshot<byte[]> _oldContentBytes = new();
    private readonly GitNestedSnapshot<string> _newContent = new();
    private readonly GitNestedSnapshot<byte[]> _newContentBytes = new();

    /// <summary>Creates a detached difference snapshot from a LibGit2Sharp change.</summary>
    /// <param name="changes">The tree entry change to copy.</param>
    /// <param name="repository">The source repository; it is used only to capture its path.</param>
    public DifferenceEntity(TreeEntryChanges changes, Repository repository)
        : this(
            repository.Info.Path,
            changes.Path,
            changes.Exists,
            changes.Status.ToString(),
            changes.OldPath,
            changes.OldMode.ToString(),
            changes.Mode.ToString(),
            changes.OldOid?.Sha,
            changes.Oid?.Sha,
            changes.Status == LibGit2Sharp.ChangeKind.Deleted)
    {
    }

    internal DifferenceEntity(
        string repositoryPath,
        string path,
        bool exists,
        string changeKind,
        string? oldPath,
        string oldMode,
        string newMode,
        string? oldSha,
        string? newSha,
        bool isDeleted)
    {
        _repositoryPath = repositoryPath;
        _path = path;
        _exists = exists;
        _changeKind = changeKind;
        _oldPath = oldPath;
        _oldMode = oldMode;
        _newMode = newMode;
        _oldSha = oldSha;
        _newSha = newSha;
        _isDeleted = isDeleted;
    }

    /// <summary>Gets the path of the changed entry in the newer tree.</summary>
    public string Path => _path;

    /// <summary>Gets whether the entry exists in the newer tree.</summary>
    public bool Exists => _exists;

    /// <summary>Gets the Git change-kind name for this entry.</summary>
    public string ChangeKind => _changeKind;

    /// <summary>Gets the path in the older tree, when Git reported one.</summary>
    public string? OldPath => _oldPath;

    /// <summary>Gets the older tree entry mode.</summary>
    public string OldMode => _oldMode;

    /// <summary>Gets the newer tree entry mode.</summary>
    public string NewMode => _newMode;

    /// <summary>Gets the older blob identifier, when one exists.</summary>
    public string? OldSha => _oldSha;

    /// <summary>Gets the newer blob identifier, when one exists.</summary>
    public string? NewSha => _newSha;

    /// <summary>Gets older blob content as text, loading it lazily from a short-lived repository scope.</summary>
    /// <remarks>The value is <see langword="null"/> when the older blob is unavailable.</remarks>
    public string? OldContent => _oldContent.GetOrCreate(() => ReadText(_oldSha));

    /// <summary>Gets older blob content as bytes, loading it lazily from a short-lived repository scope.</summary>
    /// <remarks>The value is <see langword="null"/> when the older blob is unavailable.</remarks>
    public byte[]? OldContentBytes => _oldContentBytes.GetOrCreate(() => ReadBytes(_oldSha));

    /// <summary>Gets newer blob content as text, loading it lazily from a short-lived repository scope.</summary>
    /// <remarks>Deleted entries have no newer content and return <see langword="null"/>.</remarks>
    public string? NewContent => _newContent.GetOrCreate(() => _isDeleted ? null : ReadText(_newSha));

    /// <summary>Gets newer blob content as bytes, loading it lazily from a short-lived repository scope.</summary>
    /// <remarks>Deleted entries have no newer content and return <see langword="null"/>.</remarks>
    public byte[]? NewContentBytes => _newContentBytes.GetOrCreate(() => _isDeleted ? null : ReadBytes(_newSha));

    private string? ReadText(string? sha)
    {
        if (string.IsNullOrWhiteSpace(sha))
            return null;

        using var repository = new Repository(_repositoryPath);
        return repository.Lookup<Blob>(sha)?.GetContentText();
    }

    private byte[]? ReadBytes(string? sha)
    {
        if (string.IsNullOrWhiteSpace(sha))
            return null;

        using var repository = new Repository(_repositoryPath);
        var blob = repository.Lookup<Blob>(sha);
        if (blob is null)
            return null;

        using var content = blob.GetContentStream();
        using var output = new MemoryStream();
        content.CopyTo(output);
        return output.ToArray();
    }
}
