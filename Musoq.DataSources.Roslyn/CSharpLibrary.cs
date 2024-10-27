using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.FindSymbols;
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
    
    /// <summary>
    /// Gets classes by names.
    /// </summary>
    /// <param name="entity">Injected solution entity.</param>
    /// <param name="names">The names of the classes to get.</param>
    /// <returns>Classes with the specified names.</returns>
    [BindableMethod]
    public IEnumerable<InterfaceEntity> GetInterfacesByNames([InjectSpecificSource(typeof(SolutionEntity))] SolutionEntity entity, params string[] names)
    {
        return entity.Projects.SelectMany(f => f.Documents).SelectMany(f => f.Interfaces).Where(f => names.Contains(f.Name));
    }
    
    /// <summary>
    /// Gets classes by names.
    /// </summary>
    /// <param name="entity">Injected solution entity.</param>
    /// <param name="names">The names of the classes to get.</param>
    /// <returns>Classes with the specified names.</returns>
    [BindableMethod]
    public IEnumerable<EnumEntity> GetEnumsByNames([InjectSpecificSource(typeof(SolutionEntity))] SolutionEntity entity, params string[] names)
    {
        return entity.Projects.SelectMany(f => f.Documents).SelectMany(f => f.Enums).Where(f => names.Contains(f.Name));
    }
    
    /// <summary>
    /// Finds references of the specified class entity.
    /// </summary>
    /// <param name="entity">The class entity to find references for.</param>
    /// <returns>References of the specified class entity.</returns>
    [BindableMethod]
    public IEnumerable<ReferencedDocumentEntity> FindReferences(ClassEntity entity)
    {
        var references = SymbolFinder.FindReferencesAsync(entity.Symbol, entity.Solution).Result;
        
        foreach (var reference in references)
        {
            if (!reference.Locations.Any())
                continue;
            
            foreach (var location in reference.Locations)
            {
                if (location.Document.TryGetSyntaxTree(out var tree) && location.Document.TryGetSemanticModel(out var model))
                    yield return new ReferencedDocumentEntity(location.Document, entity.Solution, tree, model, location);
            }
        }
    }
    
    /// <summary>
    /// Finds references of the specified interface entity.
    /// </summary>
    /// <param name="entity">The class entity to find references for.</param>
    /// <returns>References of the specified class entity.</returns>
    [BindableMethod]
    public IEnumerable<ReferencedDocumentEntity> FindReferences(InterfaceEntity entity)
    {
        var references = SymbolFinder.FindReferencesAsync(entity.Symbol, entity.Solution).Result;
        
        foreach (var reference in references)
        {
            if (!reference.Locations.Any())
                continue;
            
            foreach (var location in reference.Locations)
            {
                if (location.Document.TryGetSyntaxTree(out var tree) && location.Document.TryGetSemanticModel(out var model))
                    yield return new ReferencedDocumentEntity(location.Document, entity.Solution, tree, model, location);
            }
        }
    }
    
    /// <summary>
    /// Finds references of the specified interface entity.
    /// </summary>
    /// <param name="entity">The class entity to find references for.</param>
    /// <returns>References of the specified class entity.</returns>
    [BindableMethod]
    public IEnumerable<ReferencedDocumentEntity> FindReferences(EnumEntity entity)
    {
        var references = SymbolFinder.FindReferencesAsync(entity.Symbol, entity.Solution).Result;
        
        foreach (var reference in references)
        {
            if (!reference.Locations.Any())
                continue;
            
            foreach (var location in reference.Locations)
            {
                if (location.Document.TryGetSyntaxTree(out var tree) && location.Document.TryGetSemanticModel(out var model))
                    yield return new ReferencedDocumentEntity(location.Document, entity.Solution, tree, model, location);
            }
        }
    }
}