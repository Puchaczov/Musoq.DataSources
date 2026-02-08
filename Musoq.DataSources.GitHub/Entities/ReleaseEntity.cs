using Octokit;

namespace Musoq.DataSources.GitHub.Entities;

/// <summary>
/// Represents a GitHub release entity for querying.
/// </summary>
public class ReleaseEntity
{
    private readonly Release _release;

    /// <summary>
    /// Initializes a new instance of the ReleaseEntity class.
    /// </summary>
    /// <param name="release">The underlying Octokit release.</param>
    public ReleaseEntity(Release release)
    {
        _release = release;
    }

    /// <summary>
    /// Gets the release ID.
    /// </summary>
    public long Id => _release.Id;

    /// <summary>
    /// Gets the tag name.
    /// </summary>
    public string TagName => _release.TagName;

    /// <summary>
    /// Gets the release name.
    /// </summary>
    public string Name => _release.Name;

    /// <summary>
    /// Gets the release body/description.
    /// </summary>
    public string? Body => _release.Body;

    /// <summary>
    /// Gets the release URL.
    /// </summary>
    public string Url => _release.HtmlUrl;

    /// <summary>
    /// Gets the target branch or commit.
    /// </summary>
    public string TargetCommitish => _release.TargetCommitish;

    /// <summary>
    /// Gets whether this is a draft release.
    /// </summary>
    public bool Draft => _release.Draft;

    /// <summary>
    /// Gets whether this is a prerelease.
    /// </summary>
    public bool Prerelease => _release.Prerelease;

    /// <summary>
    /// Gets the author's login name.
    /// </summary>
    public string AuthorLogin => _release.Author?.Login ?? string.Empty;

    /// <summary>
    /// Gets the author's ID.
    /// </summary>
    public long? AuthorId => _release.Author?.Id;

    /// <summary>
    /// Gets the creation date.
    /// </summary>
    public DateTimeOffset CreatedAt => _release.CreatedAt;

    /// <summary>
    /// Gets the published date.
    /// </summary>
    public DateTimeOffset? PublishedAt => _release.PublishedAt;

    /// <summary>
    /// Gets the number of assets.
    /// </summary>
    public int AssetsCount => _release.Assets?.Count ?? 0;

    /// <summary>
    /// Gets the tarball URL.
    /// </summary>
    public string? TarballUrl => _release.TarballUrl;

    /// <summary>
    /// Gets the zipball URL.
    /// </summary>
    public string? ZipballUrl => _release.ZipballUrl;
}
