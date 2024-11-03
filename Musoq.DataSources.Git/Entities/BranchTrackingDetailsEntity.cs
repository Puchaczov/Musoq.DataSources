using LibGit2Sharp;

namespace Musoq.DataSources.Git.Entities;

/// <summary>
/// Represents the tracking details of a Git branch.
/// </summary>
public class BranchTrackingDetailsEntity
{
    private readonly BranchTrackingDetails _trackingDetails;

    /// <summary>
    /// Initializes a new instance of the <see cref="BranchTrackingDetailsEntity"/> class.
    /// </summary>
    /// <param name="trackingDetails">The tracking details to wrap.</param>
    public BranchTrackingDetailsEntity(BranchTrackingDetails trackingDetails)
    {
        _trackingDetails = trackingDetails;
    }

    /// <summary>
    /// Gets the number of commits the branch is ahead by.
    /// </summary>
    public int? AheadBy => _trackingDetails.AheadBy;

    /// <summary>
    /// Gets the number of commits the branch is behind by.
    /// </summary>
    public int? BehindBy => _trackingDetails.BehindBy;
}