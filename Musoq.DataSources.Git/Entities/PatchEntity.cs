using System.Collections.Generic;
using System.Linq;
using LibGit2Sharp;
using Musoq.Plugins.Attributes;

namespace Musoq.DataSources.Git.Entities;

/// <summary>
/// Represents a patch entity in a Git repository.
/// </summary>
/// <param name="patch">The patch object from LibGit2Sharp.</param>
public class PatchEntity(Patch patch)
{
    /// <summary>
    /// Gets the number of lines added in the patch.
    /// </summary>
    public int LinesAdded => patch.LinesAdded;

    /// <summary>
    /// Gets the number of lines deleted in the patch.
    /// </summary>
    public int LinesDeleted => patch.LinesDeleted;

    /// <summary>
    /// Gets the full patch file of this diff.
    /// </summary>
    public string Content => patch.Content;
    
    /// <summary>
    /// Gets the changes in the patch.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<PatchEntryChangesEntity> Changes => patch.Select(change => new PatchEntryChangesEntity(change));
}