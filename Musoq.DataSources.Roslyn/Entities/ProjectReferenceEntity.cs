using Microsoft.CodeAnalysis;

namespace Musoq.DataSources.Roslyn.Entities;

/// <summary>
/// Represents a project reference entity in the Roslyn data source.
/// </summary>
/// <param name="reference">The project reference.</param>
public class ProjectReferenceEntity(ProjectReference reference, Solution solution)
{
    private Project? _project;

    /// <summary>
    /// Gets the display name of the project reference.
    /// </summary>
    public string? Name
    {
        get
        {
            _project ??= solution.GetProject(reference.ProjectId);

            return _project?.Name;
        }
    }
}