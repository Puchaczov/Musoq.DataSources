using LibGit2Sharp;

namespace Musoq.DataSources.Git.Entities;

/// <summary>Represents a detached change in a patch.</summary>
public class PatchEntryChangesEntity
{
    private readonly int _linesAdded;
    private readonly int _linesDeleted;
    private readonly string _content;
    private readonly string _path;
    private readonly string _oldMode;
    private readonly string _mode;
    private readonly bool _isBinaryComparison;

    /// <summary>Creates a detached patch-entry snapshot from a LibGit2Sharp change.</summary>
    /// <param name="patch">The patch entry to copy.</param>
    /// <param name="repository">The source repository; it is used only while constructing the snapshot.</param>
    public PatchEntryChangesEntity(PatchEntryChanges patch, Repository repository)
        : this(patch.LinesAdded, patch.LinesDeleted, patch.Patch, patch.Path, patch.OldMode.ToString(), patch.Mode.ToString(), patch.IsBinaryComparison)
    {
    }

    internal PatchEntryChangesEntity(
        int linesAdded,
        int linesDeleted,
        string content,
        string path,
        string oldMode,
        string mode,
        bool isBinaryComparison)
    {
        _linesAdded = linesAdded;
        _linesDeleted = linesDeleted;
        _content = content;
        _path = path;
        _oldMode = oldMode;
        _mode = mode;
        _isBinaryComparison = isBinaryComparison;
    }

    /// <summary>Gets the number of added lines in this patch entry.</summary>
    public int LinesAdded => _linesAdded;

    /// <summary>Gets the number of deleted lines in this patch entry.</summary>
    public int LinesDeleted => _linesDeleted;

    /// <summary>Gets the unified diff content for this entry.</summary>
    public string Content => _content;

    /// <summary>Gets the changed path in the newer tree.</summary>
    public string Path => _path;

    /// <summary>Gets the mode in the older tree.</summary>
    public string OldMode => _oldMode;

    /// <summary>Gets the mode in the newer tree.</summary>
    public string Mode => _mode;

    /// <summary>Gets whether the comparison is binary and has no textual diff.</summary>
    public bool IsBinaryComparison => _isBinaryComparison;
}
