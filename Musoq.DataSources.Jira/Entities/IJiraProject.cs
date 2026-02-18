namespace Musoq.DataSources.Jira.Entities;

/// <summary>
///     Interface representing a Jira project entity.
/// </summary>
public interface IJiraProject
{
    /// <summary>
    ///     Gets the project ID.
    /// </summary>
    string Id { get; }

    /// <summary>
    ///     Gets the project key.
    /// </summary>
    string Key { get; }

    /// <summary>
    ///     Gets the project name.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///     Gets the project description.
    /// </summary>
    string? Description { get; }

    /// <summary>
    ///     Gets the project lead's username.
    /// </summary>
    string? Lead { get; }

    /// <summary>
    ///     Gets the project URL.
    /// </summary>
    string? Url { get; }

    /// <summary>
    ///     Gets the project category name.
    /// </summary>
    string? Category { get; }

    /// <summary>
    ///     Gets the project category description.
    /// </summary>
    string? CategoryDescription { get; }

    /// <summary>
    ///     Gets the avatar URL.
    /// </summary>
    string? AvatarUrl { get; }
}