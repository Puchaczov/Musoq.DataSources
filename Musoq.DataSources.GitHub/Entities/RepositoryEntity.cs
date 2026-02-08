using Octokit;

namespace Musoq.DataSources.GitHub.Entities;

/// <summary>
/// Represents a GitHub repository entity for querying.
/// </summary>
public class RepositoryEntity
{
    private readonly Repository _repository;

    /// <summary>
    /// Initializes a new instance of the RepositoryEntity class.
    /// </summary>
    /// <param name="repository">The underlying Octokit repository.</param>
    public RepositoryEntity(Repository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Gets the repository ID.
    /// </summary>
    public long Id => _repository.Id;

    /// <summary>
    /// Gets the repository name.
    /// </summary>
    public string Name => _repository.Name;

    /// <summary>
    /// Gets the full name of the repository (owner/name).
    /// </summary>
    public string FullName => _repository.FullName;

    /// <summary>
    /// Gets the repository description.
    /// </summary>
    public string? Description => _repository.Description;

    /// <summary>
    /// Gets the repository URL.
    /// </summary>
    public string Url => _repository.HtmlUrl;

    /// <summary>
    /// Gets the clone URL.
    /// </summary>
    public string CloneUrl => _repository.CloneUrl;

    /// <summary>
    /// Gets the SSH URL.
    /// </summary>
    public string SshUrl => _repository.SshUrl;

    /// <summary>
    /// Gets the default branch name.
    /// </summary>
    public string? DefaultBranch => _repository.DefaultBranch;

    /// <summary>
    /// Gets whether the repository is private.
    /// </summary>
    public bool IsPrivate => _repository.Private;

    /// <summary>
    /// Gets whether the repository is a fork.
    /// </summary>
    public bool IsFork => _repository.Fork;

    /// <summary>
    /// Gets whether the repository is archived.
    /// </summary>
    public bool IsArchived => _repository.Archived;

    /// <summary>
    /// Gets the primary language of the repository.
    /// </summary>
    public string? Language => _repository.Language;

    /// <summary>
    /// Gets the number of forks.
    /// </summary>
    public int ForksCount => _repository.ForksCount;

    /// <summary>
    /// Gets the number of stargazers.
    /// </summary>
    public int StargazersCount => _repository.StargazersCount;

    /// <summary>
    /// Gets the number of watchers.
    /// </summary>
    public int WatchersCount => _repository.WatchersCount;

    /// <summary>
    /// Gets the number of open issues.
    /// </summary>
    public int OpenIssuesCount => _repository.OpenIssuesCount;

    /// <summary>
    /// Gets the repository size in kilobytes.
    /// </summary>
    public long Size => _repository.Size;

    /// <summary>
    /// Gets the creation date.
    /// </summary>
    public DateTimeOffset CreatedAt => _repository.CreatedAt;

    /// <summary>
    /// Gets the last update date.
    /// </summary>
    public DateTimeOffset UpdatedAt => _repository.UpdatedAt;

    /// <summary>
    /// Gets the last push date.
    /// </summary>
    public DateTimeOffset? PushedAt => _repository.PushedAt;

    /// <summary>
    /// Gets the owner's login name.
    /// </summary>
    public string OwnerLogin => _repository.Owner?.Login ?? string.Empty;

    /// <summary>
    /// Gets the license name.
    /// </summary>
    public string? License => _repository.License?.Name;

    /// <summary>
    /// Gets the topics/tags associated with the repository.
    /// </summary>
    public IReadOnlyList<string> Topics => _repository.Topics;

    /// <summary>
    /// Gets whether issues are enabled.
    /// </summary>
    public bool HasIssues => _repository.HasIssues;

    /// <summary>
    /// Gets whether wiki is enabled.
    /// </summary>
    public bool HasWiki => _repository.HasWiki;

    /// <summary>
    /// Gets whether downloads are enabled.
    /// </summary>
    public bool HasDownloads => _repository.HasDownloads;

    /// <summary>
    /// Gets the visibility of the repository.
    /// </summary>
    public string? Visibility => _repository.Visibility?.ToString()?.ToLowerInvariant();
}
