using Musoq.DataSources.GitHub.Entities;
using Octokit;

namespace Musoq.DataSources.GitHub;

/// <summary>
/// Implementation of IGitHubApi using Octokit.
/// </summary>
internal class GitHubApi : IGitHubApi
{
    private readonly GitHubClient _client;
    private const int DefaultPerPage = 100;

    public GitHubApi(string token)
    {
        _client = new GitHubClient(new ProductHeaderValue("Musoq-GitHub-DataSource"));
        _client.Credentials = new Credentials(token);
    }
    
    public GitHubApi(GitHubClient client)
    {
        _client = client;
    }

    public async Task<IReadOnlyList<RepositoryEntity>> GetUserRepositoriesAsync(RepositoryRequest? request = null, int? perPage = null, int? page = null)
    {
        var options = new ApiOptions
        {
            PageSize = perPage ?? DefaultPerPage,
            StartPage = page ?? 1,
            PageCount = 1
        };

        var repos = await _client.Repository.GetAllForCurrent(request ?? new RepositoryRequest(), options);
        return repos.Select(r => new RepositoryEntity(r)).ToList();
    }

    public async Task<IReadOnlyList<RepositoryEntity>> GetRepositoriesForOwnerAsync(string owner, int? perPage = null, int? page = null)
    {
        var options = new ApiOptions
        {
            PageSize = perPage ?? DefaultPerPage,
            StartPage = page ?? 1,
            PageCount = 1
        };

        try
        {
            // Try to get as organization first
            var repos = await _client.Repository.GetAllForOrg(owner, options);
            return repos.Select(r => new RepositoryEntity(r)).ToList();
        }
        catch (NotFoundException)
        {
            // Fall back to user repositories
            var repos = await _client.Repository.GetAllForUser(owner, options);
            return repos.Select(r => new RepositoryEntity(r)).ToList();
        }
    }

    public async Task<RepositoryEntity> GetRepositoryAsync(string owner, string name)
    {
        var repo = await _client.Repository.Get(owner, name);
        return new RepositoryEntity(repo);
    }

    public async Task<IReadOnlyList<RepositoryEntity>> SearchRepositoriesAsync(SearchRepositoriesRequest request, int? perPage = null, int? page = null)
    {
        request.PerPage = perPage ?? DefaultPerPage;
        request.Page = page ?? 1;
        
        var result = await _client.Search.SearchRepo(request);
        return result.Items.Select(r => new RepositoryEntity(r)).ToList();
    }

    public async Task<IReadOnlyList<IssueEntity>> GetIssuesAsync(string owner, string repo, RepositoryIssueRequest? request = null, int? perPage = null, int? page = null)
    {
        var options = new ApiOptions
        {
            PageSize = perPage ?? DefaultPerPage,
            StartPage = page ?? 1,
            PageCount = 1
        };

        var issues = await _client.Issue.GetAllForRepository(owner, repo, request ?? new RepositoryIssueRequest(), options);
        return issues.Select(i => new IssueEntity(i)).ToList();
    }

    public async Task<IReadOnlyList<IssueEntity>> SearchIssuesAsync(SearchIssuesRequest request, int? perPage = null, int? page = null)
    {
        request.PerPage = perPage ?? DefaultPerPage;
        request.Page = page ?? 1;
        
        var result = await _client.Search.SearchIssues(request);
        return result.Items.Select(i => new IssueEntity(i)).ToList();
    }

    public async Task<IReadOnlyList<PullRequestEntity>> GetPullRequestsAsync(string owner, string repo, PullRequestRequest? request = null, int? perPage = null, int? page = null)
    {
        var options = new ApiOptions
        {
            PageSize = perPage ?? DefaultPerPage,
            StartPage = page ?? 1,
            PageCount = 1
        };

        var prs = await _client.PullRequest.GetAllForRepository(owner, repo, request ?? new PullRequestRequest(), options);
        
        // Get full details for each PR (to get stats like additions/deletions)
        var fullPrs = new List<PullRequestEntity>();
        foreach (var pr in prs)
        {
            try
            {
                var fullPr = await _client.PullRequest.Get(owner, repo, pr.Number);
                fullPrs.Add(new PullRequestEntity(fullPr));
            }
            catch
            {
                // If we can't get full details, use the summary
                fullPrs.Add(new PullRequestEntity(pr));
            }
        }
        
        return fullPrs;
    }

    public async Task<PullRequestEntity> GetPullRequestAsync(string owner, string repo, int number)
    {
        var pr = await _client.PullRequest.Get(owner, repo, number);
        return new PullRequestEntity(pr);
    }

    public async Task<IReadOnlyList<CommitEntity>> GetCommitsAsync(string owner, string repo, CommitRequest? request = null, int? perPage = null, int? page = null)
    {
        var options = new ApiOptions
        {
            PageSize = perPage ?? DefaultPerPage,
            StartPage = page ?? 1,
            PageCount = 1
        };

        var commits = await _client.Repository.Commit.GetAll(owner, repo, request ?? new CommitRequest(), options);
        return commits.Select(c => new CommitEntity(c)).ToList();
    }

    public async Task<IReadOnlyList<CommitEntity>> GetBranchSpecificCommitsAsync(string owner, string repo, string @base, string head)
    {
        var comparison = await _client.Repository.Commit.Compare(owner, repo, @base, head);
        return comparison.Commits.Select(c => new CommitEntity(c)).ToList();
    }

    public async Task<IReadOnlyList<BranchEntity>> GetBranchesAsync(string owner, string repo, int? perPage = null, int? page = null)
    {
        var options = new ApiOptions
        {
            PageSize = perPage ?? DefaultPerPage,
            StartPage = page ?? 1,
            PageCount = 1
        };

        var branches = await _client.Repository.Branch.GetAll(owner, repo, options);
        return branches.Select(b => new BranchEntity(b, owner, repo)).ToList();
    }

    public async Task<IReadOnlyList<ReleaseEntity>> GetReleasesAsync(string owner, string repo, int? perPage = null, int? page = null)
    {
        var options = new ApiOptions
        {
            PageSize = perPage ?? DefaultPerPage,
            StartPage = page ?? 1,
            PageCount = 1
        };

        var releases = await _client.Repository.Release.GetAll(owner, repo, options);
        return releases.Select(r => new ReleaseEntity(r)).ToList();
    }
}
