using Musoq.Schema.Diagnostics;
using Musoq.DataSources.GitHub.Entities;
using Octokit;
using Musoq.DataSources.Common;

namespace Musoq.DataSources.GitHub;

/// <summary>
///     Implementation of IGitHubApi using Octokit.
/// </summary>
internal class GitHubApi : IGitHubApi, IFilteredGitHubApi
{
    private const int DefaultPerPage = 100;
    private readonly IGitHubClient _client;
    private readonly NativePatternExecution? _patterns;
    private readonly CancellationToken _cancellation;
    private readonly Action<DataSourceDiagnostic>? _warningReporter;
    private GitHubApi(IGitHubClient client, NativePatternExecution? patterns, CancellationToken cancellationToken, Action<DataSourceDiagnostic>? warningReporter)
    { _client = client; _patterns = patterns; _cancellation = cancellationToken; _warningReporter = warningReporter; }
    public IGitHubApi WithPatterns(NativePatternExecution patterns, CancellationToken cancellationToken) => new GitHubApi(_client, patterns, cancellationToken, _warningReporter);

    internal GitHubApi WithReporting(Action<DataSourceDiagnostic>? reporter, CancellationToken token)
        => new(_client, _patterns, token, reporter);

    public GitHubApi(string token)
    {
        _client = new GitHubClient(new ProductHeaderValue("Musoq-GitHub-DataSource")) { Credentials = new Credentials(token) };
    }

    public GitHubApi(GitHubClient client) : this((IGitHubClient)client) { }

    internal GitHubApi(IGitHubClient client)
    {
        _client = client;
    }

    public async Task<IReadOnlyList<RepositoryEntity>> GetUserRepositoriesAsync(RepositoryRequest? request = null,
        int? perPage = null, int? page = null)
    {
        var options = new ApiOptions
        {
            PageSize = perPage ?? DefaultPerPage,
            StartPage = page ?? 1,
            PageCount = 1
        };

        var repos = await _client.Repository.GetAllForCurrent(request ?? new RepositoryRequest(), options);
        return PatternPage.Map(repos, _patterns, GitHubNativePatterns.Repository, static value => new RepositoryEntity(value), _cancellation);
    }

    public async Task<IReadOnlyList<RepositoryEntity>> GetRepositoriesForOwnerAsync(string owner, int? perPage = null,
        int? page = null)
    {
        var options = new ApiOptions
        {
            PageSize = perPage ?? DefaultPerPage,
            StartPage = page ?? 1,
            PageCount = 1
        };

        try
        {
            var repos = await _client.Repository.GetAllForOrg(owner, options);
            return PatternPage.Map(repos, _patterns, GitHubNativePatterns.Repository, static value => new RepositoryEntity(value), _cancellation);
        }
        catch (NotFoundException error)
        {
            _cancellation.ThrowIfCancellationRequested();
            GitHubSchema.Reports.Warn(_warningReporter, error, "The existing organization-to-user lookup fallback was used.", _cancellation);
            var repos = await _client.Repository.GetAllForUser(owner, options);
            return PatternPage.Map(repos, _patterns, GitHubNativePatterns.Repository, static value => new RepositoryEntity(value), _cancellation);
        }
    }

    public async Task<RepositoryEntity> GetRepositoryAsync(string owner, string name)
    {
        var repo = await _client.Repository.Get(owner, name);
        return new RepositoryEntity(repo);
    }

    public async Task<IReadOnlyList<RepositoryEntity>> SearchRepositoriesAsync(SearchRepositoriesRequest request,
        int? perPage = null, int? page = null)
    {
        request.PerPage = perPage ?? DefaultPerPage;
        request.Page = page ?? 1;

        var result = await _client.Search.SearchRepo(request);
        return PatternPage.Map(result.Items, _patterns, GitHubNativePatterns.Repository, static value => new RepositoryEntity(value), _cancellation);
    }

    public async Task<IReadOnlyList<IssueEntity>> GetIssuesAsync(string owner, string repo,
        RepositoryIssueRequest? request = null, int? perPage = null, int? page = null)
    {
        var options = new ApiOptions
        {
            PageSize = perPage ?? DefaultPerPage,
            StartPage = page ?? 1,
            PageCount = 1
        };

        var issues =
            await _client.Issue.GetAllForRepository(owner, repo, request ?? new RepositoryIssueRequest(), options);
        return PatternPage.Map(issues, _patterns, GitHubNativePatterns.Issue, static value => new IssueEntity(value), _cancellation);
    }

    public async Task<IReadOnlyList<IssueEntity>> SearchIssuesAsync(SearchIssuesRequest request, int? perPage = null,
        int? page = null)
    {
        request.PerPage = perPage ?? DefaultPerPage;
        request.Page = page ?? 1;

        var result = await _client.Search.SearchIssues(request);
        return PatternPage.Map(result.Items, _patterns, GitHubNativePatterns.Issue, static value => new IssueEntity(value), _cancellation);
    }

    public async Task<IReadOnlyList<PullRequestEntity>> GetPullRequestsAsync(string owner, string repo,
        PullRequestRequest? request = null, int? perPage = null, int? page = null)
    {
        var options = new ApiOptions
        {
            PageSize = perPage ?? DefaultPerPage,
            StartPage = page ?? 1,
            PageCount = 1
        };

        var prs = await _client.PullRequest.GetAllForRepository(owner, repo, request ?? new PullRequestRequest(),
            options);


        var fullPrs = new List<PullRequestEntity>();
        _patterns?.PageRead();
        foreach (var pr in prs)
        {
            _cancellation.ThrowIfCancellationRequested();
            PullRequest value;
            try { value = await _client.PullRequest.Get(owner, repo, pr.Number); }
            catch (Exception error) when (GitHubSchema.Reports.Warn(_warningReporter, error, "The existing lightweight pull-request fallback was used.", _cancellation)) { value = pr; }
            _cancellation.ThrowIfCancellationRequested();
            if (_patterns?.Matches(value, GitHubNativePatterns.PullRequest) == false) continue;
            fullPrs.Add(new PullRequestEntity(value));
            _patterns?.Constructed();
        }
        return _patterns is null ? fullPrs : new PatternPage<PullRequestEntity>(fullPrs, prs.Count);
    }

    public async Task<PullRequestEntity> GetPullRequestAsync(string owner, string repo, int number)
    {
        var pr = await _client.PullRequest.Get(owner, repo, number);
        return new PullRequestEntity(pr);
    }

    public async Task<IReadOnlyList<CommitEntity>> GetCommitsAsync(string owner, string repo,
        CommitRequest? request = null, int? perPage = null, int? page = null)
    {
        var options = new ApiOptions
        {
            PageSize = perPage ?? DefaultPerPage,
            StartPage = page ?? 1,
            PageCount = 1
        };

        var commits = await _client.Repository.Commit.GetAll(owner, repo, request ?? new CommitRequest(), options);
        return PatternPage.Map(commits, _patterns, GitHubNativePatterns.Commit, static value => new CommitEntity(value), _cancellation);
    }

    public async Task<IReadOnlyList<CommitEntity>> GetBranchSpecificCommitsAsync(string owner, string repo,
        string @base, string head)
    {
        var comparison = await _client.Repository.Commit.Compare(owner, repo, @base, head);
        return PatternPage.Map(comparison.Commits, _patterns, GitHubNativePatterns.Commit, static value => new CommitEntity(value), _cancellation);
    }

    public async Task<IReadOnlyList<BranchEntity>> GetBranchesAsync(string owner, string repo, int? perPage = null,
        int? page = null)
    {
        var options = new ApiOptions
        {
            PageSize = perPage ?? DefaultPerPage,
            StartPage = page ?? 1,
            PageCount = 1
        };

        var branches = await _client.Repository.Branch.GetAll(owner, repo, options);
        return PatternPage.Map(branches.Select(value => new GitHubNativePatterns.BranchInput(value, owner, repo)), _patterns,
            GitHubNativePatterns.Branch, static value => new BranchEntity(value.Value, value.Owner, value.Repository), _cancellation);
    }

    public async Task<IReadOnlyList<ReleaseEntity>> GetReleasesAsync(string owner, string repo, int? perPage = null,
        int? page = null)
    {
        var options = new ApiOptions
        {
            PageSize = perPage ?? DefaultPerPage,
            StartPage = page ?? 1,
            PageCount = 1
        };

        var releases = await _client.Repository.Release.GetAll(owner, repo, options);
        return PatternPage.Map(releases, _patterns, GitHubNativePatterns.Release, static value => new ReleaseEntity(value), _cancellation);
    }
}