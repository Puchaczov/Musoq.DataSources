using LibGit2Sharp;

namespace Musoq.DataSources.Git.Entities;

/// <summary>
/// Represents a stash entity in a Git repository.
/// </summary>
public class StashEntity
{
    private readonly Stash _stash;

    /// <summary>
    /// Initializes a new instance of the <see cref="StashEntity"/> class.
    /// </summary>
    /// <param name="stash">The stash object from LibGit2Sharp.</param>
    public StashEntity(Stash stash)
    {
        _stash = stash;
    }

    /// <summary>
    /// Gets the stash message.
    /// </summary>
    public string Message => _stash.Message;

    /// <summary>
    /// Gets the commit entity representing the index state of the stash.
    /// </summary>
    public CommitEntity Index => new(_stash.Index);

    /// <summary>
    /// Gets the commit entity representing the work tree state of the stash.
    /// </summary>
    public CommitEntity WorkTree => new(_stash.WorkTree);

    /// <summary>
    /// Gets the commit entity representing the untracked files state of the stash.
    /// </summary>
    public CommitEntity UntrackedFiles => new(_stash.Untracked);
}