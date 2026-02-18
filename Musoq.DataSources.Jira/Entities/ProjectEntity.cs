using Atlassian.Jira;

namespace Musoq.DataSources.Jira.Entities;

/// <summary>
///     Represents a Jira project entity for querying.
/// </summary>
public class ProjectEntity : IJiraProject
{
    private readonly Project _project;

    /// <summary>
    ///     Initializes a new instance of the ProjectEntity class.
    /// </summary>
    /// <param name="project">The underlying Atlassian.SDK project.</param>
    public ProjectEntity(Project project)
    {
        _project = project;
    }

    /// <summary>
    ///     Gets the project ID.
    /// </summary>
    public string Id => _project.Id ?? string.Empty;

    /// <summary>
    ///     Gets the project key.
    /// </summary>
    public string Key => _project.Key ?? string.Empty;

    /// <summary>
    ///     Gets the project name.
    /// </summary>
    public string Name => _project.Name ?? string.Empty;

    /// <summary>
    ///     Gets the project description.
    /// </summary>
    public string? Description => null; // Description not available in Project class

    /// <summary>
    ///     Gets the project lead username.
    /// </summary>
    public string? Lead => _project.Lead;

    /// <summary>
    ///     Gets the project URL.
    /// </summary>
    public string? Url => _project.Url;

    /// <summary>
    ///     Gets the project category name.
    /// </summary>
    public string? Category => null; // ProjectCategory not available in Project class

    /// <summary>
    ///     Gets the project category description.
    /// </summary>
    public string? CategoryDescription => null; // ProjectCategory not available in Project class

    /// <summary>
    ///     Gets the avatar URL.
    /// </summary>
    public string? AvatarUrl => _project.AvatarUrls?.Large;
}