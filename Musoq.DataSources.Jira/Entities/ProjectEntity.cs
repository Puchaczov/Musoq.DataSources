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
    public string Id => ReadId(_project);

    /// <summary>
    ///     Gets the project key.
    /// </summary>
    public string Key => ReadKey(_project);

    /// <summary>
    ///     Gets the project name.
    /// </summary>
    public string Name => ReadName(_project);

    /// <summary>
    ///     Gets the project description.
    /// </summary>
    public string? Description => ReadDescription(_project); // Description not available in Project class

    /// <summary>
    ///     Gets the project lead username.
    /// </summary>
    public string? Lead => ReadLead(_project);

    /// <summary>
    ///     Gets the project URL.
    /// </summary>
    public string? Url => ReadUrl(_project);

    /// <summary>
    ///     Gets the project category name.
    /// </summary>
    public string? Category => ReadCategory(_project); // ProjectCategory not available in Project class

    /// <summary>
    ///     Gets the project category description.
    /// </summary>
    public string? CategoryDescription => ReadCategoryDescription(_project); // ProjectCategory not available in Project class

    /// <summary>
    ///     Gets the avatar URL.
    /// </summary>
    public string? AvatarUrl => ReadAvatarUrl(_project);

    internal static string ReadId(Project value) => value.Id ?? string.Empty;
    internal static string ReadKey(Project value) => value.Key ?? string.Empty;
    internal static string ReadName(Project value) => value.Name ?? string.Empty;
    internal static string? ReadDescription(Project value) => null;
    internal static string? ReadLead(Project value) => value.Lead;
    internal static string? ReadUrl(Project value) => value.Url;
    internal static string? ReadCategory(Project value) => null;
    internal static string? ReadCategoryDescription(Project value) => null;
    internal static string? ReadAvatarUrl(Project value) => value.AvatarUrls?.Large;
}