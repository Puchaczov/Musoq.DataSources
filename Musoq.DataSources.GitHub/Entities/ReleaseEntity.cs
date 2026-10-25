using Octokit;

namespace Musoq.DataSources.GitHub.Entities;

/// <summary>
///     Represents a GitHub release entity for querying.
/// </summary>
public class ReleaseEntity
{
    private readonly Release _release;

    /// <summary>
    ///     Initializes a new instance of the ReleaseEntity class.
    /// </summary>
    /// <param name="release">The underlying Octokit release.</param>
    public ReleaseEntity(Release release)
    {
        _release = release;
    }

    /// <summary>
    ///     Gets the release ID.
    /// </summary>
    public long Id => _release.Id;

    /// <summary>
    ///     Gets the tag name.
    /// </summary>
    public string TagName => ReadTagName(_release);

    /// <summary>
    ///     Gets the release name.
    /// </summary>
    public string Name => ReadName(_release);

    /// <summary>
    ///     Gets the release body/description.
    /// </summary>
    public string? Body => ReadBody(_release);

    /// <summary>
    ///     Gets the release URL.
    /// </summary>
    public string Url => ReadUrl(_release);

    /// <summary>
    ///     Gets the target branch or commit.
    /// </summary>
    public string TargetCommitish => ReadTargetCommitish(_release);

    /// <summary>
    ///     Gets whether this is a draft release.
    /// </summary>
    public bool Draft => _release.Draft;

    /// <summary>
    ///     Gets whether this is a prerelease.
    /// </summary>
    public bool Prerelease => _release.Prerelease;

    /// <summary>
    ///     Gets the author's login name.
    /// </summary>
    public string AuthorLogin => ReadAuthorLogin(_release);

    /// <summary>
    ///     Gets the author's ID.
    /// </summary>
    public long? AuthorId => _release.Author?.Id;

    /// <summary>
    ///     Gets the creation date.
    /// </summary>
    public DateTimeOffset CreatedAt => _release.CreatedAt;

    /// <summary>
    ///     Gets the published date.
    /// </summary>
    public DateTimeOffset? PublishedAt => _release.PublishedAt;

    /// <summary>
    ///     Gets the number of assets.
    /// </summary>
    public int AssetsCount => _release.Assets?.Count ?? 0;

    /// <summary>
    ///     Gets the tarball URL.
    /// </summary>
    public string? TarballUrl => ReadTarballUrl(_release);

    /// <summary>
    ///     Gets the zipball URL.
    /// </summary>
    public string? ZipballUrl => ReadZipballUrl(_release);

    internal static string ReadTagName(Release value) => value.TagName;
    internal static string ReadName(Release value) => value.Name;
    internal static string? ReadBody(Release value) => value.Body;
    internal static string ReadUrl(Release value) => value.HtmlUrl;
    internal static string ReadTargetCommitish(Release value) => value.TargetCommitish;
    internal static string ReadAuthorLogin(Release value) => value.Author?.Login ?? string.Empty;
    internal static string? ReadTarballUrl(Release value) => value.TarballUrl;
    internal static string? ReadZipballUrl(Release value) => value.ZipballUrl;
}