using System.Collections.Generic;
using System.Linq;
using Musoq.DataSources.Roslyn.Entities;
using Musoq.Plugins;
using Musoq.Plugins.Attributes;

namespace Musoq.DataSources.Roslyn;

/// <inheritdoc />
public class CSharpLibrary : LibraryBase
{
    /// <summary>
    /// Gets projects by names.
    /// </summary>
    /// <param name="entity">Injected solution entity.</param>
    /// <param name="names">The names of the projects to get.</param>
    /// <returns>Projects with the specified names.</returns>
    [BindableMethod]
    public IEnumerable<ProjectEntity> GetProjectsByNames([InjectSpecificSource(typeof(SolutionEntity))] SolutionEntity entity, params string[] names)
    {
        return entity.Projects.Where(f => names.Contains(f.Name));
    }
    
    /// <summary>
    /// Gets classes by names.
    /// </summary>
    /// <param name="entity">Injected solution entity.</param>
    /// <param name="names">The names of the classes to get.</param>
    /// <returns>Classes with the specified names.</returns>
    [BindableMethod]
    public IEnumerable<ClassEntity> GetClassesByNames([InjectSpecificSource(typeof(SolutionEntity))] SolutionEntity entity, params string[] names)
    {
        return entity.Projects.SelectMany(f => f.Documents).SelectMany(f => f.Classes).Where(f => names.Contains(f.Name));
    }
}