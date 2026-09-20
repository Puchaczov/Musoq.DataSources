using LibGit2Sharp;

namespace Musoq.DataSources.Git.Entities;

/// <summary>
///     Represents the tracking details of a Git branch.
/// </summary>
public class BranchTrackingDetailsEntity
{
    private readonly int? _aheadBy;
    private readonly int? _behindBy;

    /// <summary>
    ///     Initializes a new instance of the <see cref="BranchTrackingDetailsEntity" /> class.
    /// </summary>
    /// <param name="trackingDetails">The tracking details to wrap.</param>
    /// <param name="repository">The Git repository.</param>
    public BranchTrackingDetailsEntity(BranchTrackingDetails trackingDetails, Repository repository)
    {
        _aheadBy = trackingDetails?.AheadBy;
        _behindBy = trackingDetails?.BehindBy;
    }

    internal BranchTrackingDetailsEntity(int? aheadBy, int? behindBy)
    {
        _aheadBy = aheadBy;
        _behindBy = behindBy;
    }

    /// <summary>
    ///     Gets the number of commits the branch is ahead by.
    /// </summary>
    public int? AheadBy => _aheadBy;

    /// <summary>
    ///     Gets the number of commits the branch is behind by.
    /// </summary>
    public int? BehindBy => _behindBy;
}
