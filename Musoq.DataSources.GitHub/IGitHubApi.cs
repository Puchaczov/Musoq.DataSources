using Musoq.DataSources.GitHub.Entities;
using Octokit;

namespace Musoq.DataSources.GitHub;

/// <summary>
/// Interface for GitHub API operations. Abstracted for testability.
/// </summary>
internal interface IGitHubApi
{
    /// <summary>
    /// Gets repositories for the authenticated user.
    /// </summary>
    Task<IReadOnlyList<RepositoryEntity>> GetUserRepositoriesAsync(RepositoryRequest? request = null, int? perPage = null, int? page = null);
    
    /// <summary>
    /// Gets repositories for a specific owner.
    /// </summary>
    Task<IReadOnlyList<RepositoryEntity>> GetRepositoriesForOwnerAsync(string owner, int? perPage = null, int? page = null);
    
    /// <summary>
    /// Gets a specific repository.
    /// </summary>
    Task<RepositoryEntity> GetRepositoryAsync(string owner, string name);
    
    /// <summary>
    /// Searches repositories with optional query.
    /// </summary>
    Task<IReadOnlyList<RepositoryEntity>> SearchRepositoriesAsync(SearchRepositoriesRequest request, int? perPage = null, int? page = null);
    
    /// <summary>
    /// Gets issues for a repository.
    /// </summary>
    Task<IReadOnlyList<IssueEntity>> GetIssuesAsync(string owner, string repo, RepositoryIssueRequest? request = null, int? perPage = null, int? page = null);
    
    /// <summary>
    /// Searches issues with optional query.
    /// </summary>
    Task<IReadOnlyList<IssueEntity>> SearchIssuesAsync(SearchIssuesRequest request, int? perPage = null, int? page = null);
    
    /// <summary>
    /// Gets pull requests for a repository.
    /// </summary>
    Task<IReadOnlyList<PullRequestEntity>> GetPullRequestsAsync(string owner, string repo, PullRequestRequest? request = null, int? perPage = null, int? page = null);
    
    /// <summary>
    /// Gets a specific pull request with full details.
    /// </summary>
    Task<PullRequestEntity> GetPullRequestAsync(string owner, string repo, int number);
    
    /// <summary>
    /// Gets commits for a repository.
    /// </summary>
    Task<IReadOnlyList<CommitEntity>> GetCommitsAsync(string owner, string repo, CommitRequest? request = null, int? perPage = null, int? page = null);
    
    /// <summary>
    /// Gets branches for a repository.
    /// </summary>
    Task<IReadOnlyList<BranchEntity>> GetBranchesAsync(string owner, string repo, int? perPage = null, int? page = null);
    
    /// <summary>
    /// Gets releases for a repository.
    /// </summary>
    Task<IReadOnlyList<ReleaseEntity>> GetReleasesAsync(string owner, string repo, int? perPage = null, int? page = null);
}
