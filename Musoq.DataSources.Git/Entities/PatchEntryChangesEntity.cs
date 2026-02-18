using LibGit2Sharp;

namespace Musoq.DataSources.Git.Entities;

/// <summary>
///     Represents the changes in a patch entry in a Git repository.
/// </summary>
/// <param name="patch">The patch entry changes object from LibGit2Sharp.</param>
public class PatchEntryChangesEntity(PatchEntryChanges patch, Repository repository)
{
    internal readonly Repository LibGitRepository = repository;

    /// <summary>
    ///     Gets the number of lines added in the patch entry.
    /// </summary>
    public int LinesAdded => patch.LinesAdded;

    /// <summary>
    ///     Gets the number of lines deleted in the patch entry.
    /// </summary>
    public int LinesDeleted => patch.LinesDeleted;

    /// <summary>
    ///     Gets the patch content.
    /// </summary>
    public string Content => patch.Patch;

    /// <summary>
    ///     Gets the file path of the patch entry.
    /// </summary>
    public string Path => patch.Path;

    /// <summary>
    ///     Gets the old file mode of the patch entry.
    /// </summary>
    public string OldMode => patch.OldMode.ToString();

    /// <summary>
    ///     Gets the new file mode of the patch entry.
    /// </summary>
    public string Mode => patch.Mode.ToString();

    /// <summary>
    ///     Gets a value indicating whether the patch entry is a binary comparison.
    /// </summary>
    public bool IsBinaryComparison => patch.IsBinaryComparison;
}