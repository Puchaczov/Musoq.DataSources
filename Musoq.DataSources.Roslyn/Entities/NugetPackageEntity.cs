namespace Musoq.DataSources.Roslyn.Entities;

/// <summary>
/// Represents a NuGet package entity with its basic properties.
/// </summary>
public class NugetPackageEntity
{
    /// <summary>
    /// Gets the ID of the NuGet package.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the version of the NuGet package.
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// Gets the license URL of the NuGet package.
    /// </summary>
    public string? LicenseUrl { get; }

    /// <summary>
    /// Gets the project URL of the NuGet package.
    /// </summary>
    public string? ProjectUrl { get; }

    /// <summary>
    /// Gets the title of the NuGet package.
    /// </summary>
    public string? Title { get; }

    /// <summary>
    /// Gets the authors of the NuGet package.
    /// </summary>
    public string? Authors { get; }

    /// <summary>
    /// Gets the owners of the NuGet package.
    /// </summary>
    public string? Owners { get; }

    /// <summary>
    /// Gets a value indicating whether the NuGet package requires license acceptance.
    /// </summary>
    public bool? RequireLicenseAcceptance { get; }

    /// <summary>
    /// Gets the description of the NuGet package.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Gets the summary of the NuGet package.
    /// </summary>
    public string? Summary { get; }

    /// <summary>
    /// Gets the release notes of the NuGet package.
    /// </summary>
    public string? ReleaseNotes { get; }

    /// <summary>
    /// Gets the copyright information of the NuGet package.
    /// </summary>
    public string? Copyright { get; }

    /// <summary>
    /// Gets the language of the NuGet package.
    /// </summary>
    public string? Language { get; }

    /// <summary>
    /// Gets the tags of the NuGet package.
    /// </summary>
    public string? Tags { get; }
    
    /// <summary>
    /// Gets the license content of the NuGet package.
    /// </summary>
    public string? LicenseContent { get; }
    
    /// <summary>
    /// Gets the license of the NuGet package.
    /// </summary>
    public string? License { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NugetPackageEntity"/> class.
    /// </summary>
    /// <param name="id">The ID of the NuGet package.</param>
    /// <param name="version">The version of the NuGet package.</param>
    /// <param name="licenseUrl">The license URL of the NuGet package.</param>
    /// <param name="projectUrl">The project URL of the NuGet package.</param>
    /// <param name="title">The title of the NuGet package.</param>
    /// <param name="authors">The authors of the NuGet package.</param>
    /// <param name="owners">The owners of the NuGet package.</param>
    /// <param name="requireLicenseAcceptance">A value indicating whether the NuGet package requires license acceptance.</param>
    /// <param name="description">The description of the NuGet package.</param>
    /// <param name="summary">The summary of the NuGet package.</param>
    /// <param name="releaseNotes">The release notes of the NuGet package.</param>
    /// <param name="copyright">The copyright information of the NuGet package.</param>
    /// <param name="language">The language of the NuGet package.</param>
    /// <param name="tags">The tags of the NuGet package.</param>
    /// <param name="licenseContent">The license content of the NuGet package.</param>
    /// <param name="license">The license of the NuGet package.</param>
    public NugetPackageEntity(
        string id, 
        string version, 
        string? licenseUrl, 
        string? projectUrl, 
        string? title, 
        string? authors, 
        string? owners, 
        bool? requireLicenseAcceptance, 
        string? description, 
        string? summary, 
        string? releaseNotes, 
        string? copyright, 
        string? language, 
        string? tags,
        string? licenseContent,
        string? license)
    {
        Id = id;
        Version = version;
        LicenseUrl = licenseUrl;
        ProjectUrl = projectUrl;
        Title = title;
        Authors = authors;
        Owners = owners;
        RequireLicenseAcceptance = requireLicenseAcceptance;
        Description = description;
        Summary = summary;
        ReleaseNotes = releaseNotes;
        Copyright = copyright;
        Language = language;
        Tags = tags;
        LicenseContent = licenseContent;
        License = license;
    }
}