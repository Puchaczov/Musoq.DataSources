using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
    public IEnumerable<ReferencedDocumentEntity> FindReferences([InjectSpecificSource(typeof(ClassEntity))] ClassEntity entity)
    {
        var references = RoslynAsyncHelper.RunSyncWithTimeout(
            ct => SymbolFinder.FindReferencesAsync(entity.Symbol, entity.Solution, ct),
            RoslynAsyncHelper.DefaultReferenceTimeout,
            defaultValue: null);
        
        if (references == null)
            yield break;
        
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
    public IEnumerable<ReferencedDocumentEntity> FindReferences([InjectSpecificSource(typeof(InterfaceEntity))] InterfaceEntity entity)
    {
        var references = RoslynAsyncHelper.RunSyncWithTimeout(
            ct => SymbolFinder.FindReferencesAsync(entity.Symbol, entity.Solution, ct),
            RoslynAsyncHelper.DefaultReferenceTimeout,
            defaultValue: null);
        
        if (references == null)
            yield break;
        
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
    public IEnumerable<ReferencedDocumentEntity> FindReferences([InjectSpecificSource(typeof(EnumEntity))] EnumEntity entity)
    {
        var references = RoslynAsyncHelper.RunSyncWithTimeout(
            ct => SymbolFinder.FindReferencesAsync(entity.Symbol, entity.Solution, ct),
            RoslynAsyncHelper.DefaultReferenceTimeout,
            defaultValue: null);
        
        if (references == null)
            yield break;
        
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
    /// Gets the NuGet packages for the specified project entity.
    /// </summary>
    /// <param name="project">The project entity to get NuGet packages for.</param>
    /// <param name="withTransitivePackages"> <c>true</c> to include transitive packages; otherwise, <c>false</c>.</param>
    /// <returns>NuGet packages for the specified project entity.</returns>
    [BindableMethod]
    public IEnumerable<NugetPackageEntity> GetNugetPackages([InjectSpecificSource(typeof(ProjectEntity))] ProjectEntity project, bool withTransitivePackages)
    {
        var nugetPackageEntities = project.NugetPackageEntities;
        
        if (nugetPackageEntities != null)
            return nugetPackageEntities;

        return RoslynAsyncHelper.RunSync(GetNugetPackagesAsync(project, withTransitivePackages));
    }

    /// <summary>
    /// Gets structs by names.
    /// </summary>
    /// <param name="entity">Injected solution entity.</param>
    /// <param name="names">The names of the structs to get.</param>
    /// <returns>Structs with the specified names.</returns>
    [BindableMethod]
    public IEnumerable<StructEntity> GetStructsByNames([InjectSpecificSource(typeof(SolutionEntity))] SolutionEntity entity, params string[] names)
    {
        return entity.Projects.SelectMany(f => f.Documents).SelectMany(f => f.Structs).Where(f => names.Contains(f.Name));
    }

    /// <summary>
    /// Gets methods by names from all classes in the solution.
    /// </summary>
    /// <param name="entity">Injected solution entity.</param>
    /// <param name="names">The names of the methods to get.</param>
    /// <returns>Methods with the specified names.</returns>
    [BindableMethod]
    public IEnumerable<MethodEntity> GetMethodsByNames([InjectSpecificSource(typeof(SolutionEntity))] SolutionEntity entity, params string[] names)
    {
        return entity.Projects
            .SelectMany(p => p.Documents)
            .SelectMany(d => d.Classes)
            .SelectMany(c => c.Methods)
            .Where(m => names.Contains(m.Name));
    }

    /// <summary>
    /// Finds references of the specified method entity.
    /// </summary>
    /// <param name="entity">The method entity to find references for.</param>
    /// <returns>References of the specified method entity.</returns>
    [BindableMethod]
    public IEnumerable<ReferencedDocumentEntity> FindReferences([InjectSpecificSource(typeof(MethodEntity))] MethodEntity entity)
    {
        if (entity.Solution == null)
            yield break;

        var references = RoslynAsyncHelper.RunSyncWithTimeout(
            ct => SymbolFinder.FindReferencesAsync(entity.Symbol, entity.Solution, ct),
            RoslynAsyncHelper.DefaultReferenceTimeout,
            defaultValue: null);
        
        if (references == null)
            yield break;
        
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
    /// Finds references of the specified struct entity.
    /// </summary>
    /// <param name="entity">The struct entity to find references for.</param>
    /// <returns>References of the specified struct entity.</returns>
    [BindableMethod]
    public IEnumerable<ReferencedDocumentEntity> FindReferences([InjectSpecificSource(typeof(StructEntity))] StructEntity entity)
    {
        var references = RoslynAsyncHelper.RunSyncWithTimeout(
            ct => SymbolFinder.FindReferencesAsync(entity.Symbol, entity.Solution, ct),
            RoslynAsyncHelper.DefaultReferenceTimeout,
            defaultValue: null);
        
        if (references == null)
            yield break;
        
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
    /// Finds all implementations of the specified interface.
    /// </summary>
    /// <param name="entity">Injected solution entity.</param>
    /// <param name="interfaceEntity">The interface entity to find implementations for.</param>
    /// <returns>Classes that implement the specified interface.</returns>
    [BindableMethod]
    public IEnumerable<ClassEntity> FindImplementations([InjectSpecificSource(typeof(SolutionEntity))] SolutionEntity entity, InterfaceEntity interfaceEntity)
    {
        var implementations = RoslynAsyncHelper.RunSync(SymbolFinder.FindImplementationsAsync(interfaceEntity.Symbol, interfaceEntity.Solution));
        
        foreach (var implementation in implementations)
        {
            if (implementation is not Microsoft.CodeAnalysis.INamedTypeSymbol typeSymbol)
                continue;
                
            foreach (var syntaxRef in typeSymbol.DeclaringSyntaxReferences)
            {
                var syntax = syntaxRef.GetSyntax();
                if (syntax is not Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax classDecl)
                    continue;
                    
                var tree = syntax.SyntaxTree;
                var documentForModel = interfaceEntity.Solution.Projects
                    .SelectMany(p => p.Documents)
                    .FirstOrDefault(d => d.FilePath == tree.FilePath);

                var compilation = documentForModel == null
                    ? null
                    : RoslynAsyncHelper.RunSync(documentForModel.GetSemanticModelAsync());
                    
                if (compilation == null)
                    continue;
                    
                var document = interfaceEntity.Solution.Projects
                    .SelectMany(p => p.Documents)
                    .FirstOrDefault(d => d.FilePath == tree.FilePath);
                    
                if (document == null)
                    continue;
                    
                yield return new ClassEntity(typeSymbol, classDecl, compilation, interfaceEntity.Solution, new DocumentEntity(document, interfaceEntity.Solution));
            }
        }
    }

    /// <summary>
    /// Finds all classes that derive from the specified class.
    /// </summary>
    /// <param name="entity">Injected solution entity.</param>
    /// <param name="classEntity">The class entity to find derived classes for.</param>
    /// <returns>Classes that derive from the specified class.</returns>
    [BindableMethod]
    public IEnumerable<ClassEntity> FindDerivedClasses([InjectSpecificSource(typeof(SolutionEntity))] SolutionEntity entity, ClassEntity classEntity)
    {
        var derivedClasses = RoslynAsyncHelper.RunSync(SymbolFinder.FindDerivedClassesAsync(classEntity.Symbol, classEntity.Solution));
        
        foreach (var derived in derivedClasses)
        {
            foreach (var syntaxRef in derived.DeclaringSyntaxReferences)
            {
                var syntax = syntaxRef.GetSyntax();
                if (syntax is not Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax classDecl)
                    continue;
                    
                var tree = syntax.SyntaxTree;
                var document = classEntity.Solution.Projects
                    .SelectMany(p => p.Documents)
                    .FirstOrDefault(d => d.FilePath == tree.FilePath);
                    
                if (document == null)
                    continue;
                    
                var semanticModel = RoslynAsyncHelper.RunSync(document.GetSemanticModelAsync());
                if (semanticModel == null)
                    continue;
                    
                yield return new ClassEntity(derived, classDecl, semanticModel, classEntity.Solution, new DocumentEntity(document, classEntity.Solution));
            }
        }
    }

    /// <summary>
    /// Gets methods by name pattern across the solution.
    /// </summary>
    /// <param name="entity">Injected solution entity.</param>
    /// <param name="namePattern">The name pattern to match (supports * wildcard).</param>
    /// <returns>Methods matching the specified name pattern.</returns>
    [BindableMethod]
    public IEnumerable<MethodEntity> GetMethodsByPattern([InjectSpecificSource(typeof(SolutionEntity))] SolutionEntity entity, string namePattern)
    {
        var pattern = "^" + System.Text.RegularExpressions.Regex.Escape(namePattern).Replace("\\*", ".*") + "$";
        var regex = new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        return entity.Projects
            .SelectMany(p => p.Documents)
            .SelectMany(d => d.Classes)
            .SelectMany(c => c.Methods)
            .Where(m => regex.IsMatch(m.Name));
    }

    /// <summary>
    /// Gets all classes with cyclomatic complexity above the specified threshold.
    /// </summary>
    /// <param name="entity">Injected solution entity.</param>
    /// <param name="threshold">The minimum weighted methods per class to filter by.</param>
    /// <returns>Classes with high complexity.</returns>
    [BindableMethod]
    public IEnumerable<ClassEntity> GetHighComplexityClasses([InjectSpecificSource(typeof(SolutionEntity))] SolutionEntity entity, int threshold)
    {
        return entity.Projects
            .SelectMany(p => p.Documents)
            .SelectMany(d => d.Classes)
            .Where(c => c.WeightedMethodsPerClass >= threshold);
    }

    /// <summary>
    /// Gets all methods with cyclomatic complexity above the specified threshold.
    /// </summary>
    /// <param name="entity">Injected solution entity.</param>
    /// <param name="threshold">The minimum cyclomatic complexity to filter by.</param>
    /// <returns>Methods with high complexity.</returns>
    [BindableMethod]
    public IEnumerable<MethodEntity> GetHighComplexityMethods([InjectSpecificSource(typeof(SolutionEntity))] SolutionEntity entity, int threshold)
    {
        return entity.Projects
            .SelectMany(p => p.Documents)
            .SelectMany(d => d.Classes)
            .SelectMany(c => c.Methods)
            .Where(m => m.CyclomaticComplexity >= threshold);
    }

    /// <summary>
    /// Gets all classes that lack documentation.
    /// </summary>
    /// <param name="entity">Injected solution entity.</param>
    /// <returns>Classes without XML documentation.</returns>
    [BindableMethod]
    public IEnumerable<ClassEntity> GetUndocumentedClasses([InjectSpecificSource(typeof(SolutionEntity))] SolutionEntity entity)
    {
        return entity.Projects
            .SelectMany(p => p.Documents)
            .SelectMany(d => d.Classes)
            .Where(c => !c.HasDocumentation);
    }

    /// <summary>
    /// Gets all methods that lack documentation.
    /// </summary>
    /// <param name="entity">Injected solution entity.</param>
    /// <returns>Methods without XML documentation.</returns>
    [BindableMethod]
    public IEnumerable<MethodEntity> GetUndocumentedMethods([InjectSpecificSource(typeof(SolutionEntity))] SolutionEntity entity)
    {
        return entity.Projects
            .SelectMany(p => p.Documents)
            .SelectMany(d => d.Classes)
            .SelectMany(c => c.Methods)
            .Where(m => !m.HasDocumentation);
    }

    /// <summary>
    /// Gets all async methods that don't follow the Async suffix naming convention.
    /// </summary>
    /// <param name="entity">Injected solution entity.</param>
    /// <returns>Async methods without the Async suffix.</returns>
    [BindableMethod]
    public IEnumerable<MethodEntity> GetAsyncMethodsWithoutSuffix([InjectSpecificSource(typeof(SolutionEntity))] SolutionEntity entity)
    {
        return entity.Projects
            .SelectMany(p => p.Documents)
            .SelectMany(d => d.Classes)
            .SelectMany(c => c.Methods)
            .Where(m => m.IsAsync && !m.Name.EndsWith("Async"));
    }

    /// <summary>
    /// Gets all methods with deep nesting.
    /// </summary>
    /// <param name="entity">Injected solution entity.</param>
    /// <param name="maxDepth">The maximum allowed nesting depth.</param>
    /// <returns>Methods with nesting depth exceeding the threshold.</returns>
    [BindableMethod]
    public IEnumerable<MethodEntity> GetDeeplyNestedMethods([InjectSpecificSource(typeof(SolutionEntity))] SolutionEntity entity, int maxDepth)
    {
        return entity.Projects
            .SelectMany(p => p.Documents)
            .SelectMany(d => d.Classes)
            .SelectMany(c => c.Methods)
            .Where(m => m.MaxNestingDepth > maxDepth);
    }

    /// <summary>
    /// Gets all classes with low cohesion (high LCOM value).
    /// </summary>
    /// <param name="entity">Injected solution entity.</param>
    /// <param name="threshold">The minimum LCOM value to filter by.</param>
    /// <returns>Classes with low cohesion.</returns>
    [BindableMethod]
    public IEnumerable<ClassEntity> GetLowCohesionClasses([InjectSpecificSource(typeof(SolutionEntity))] SolutionEntity entity, double threshold)
    {
        return entity.Projects
            .SelectMany(p => p.Documents)
            .SelectMany(d => d.Classes)
            .Where(c => c.LackOfCohesion >= threshold);
    }

    /// <summary>
    /// Gets all methods that have unused parameters.
    /// </summary>
    /// <param name="entity">Injected solution entity.</param>
    /// <returns>Methods with at least one unused parameter.</returns>
    [BindableMethod]
    public IEnumerable<MethodEntity> GetMethodsWithUnusedParameters([InjectSpecificSource(typeof(SolutionEntity))] SolutionEntity entity)
    {
        return entity.Projects
            .SelectMany(p => p.Documents)
            .SelectMany(d => d.Classes)
            .SelectMany(c => c.Methods)
            .Where(m => m.UnusedParameterCount > 0);
    }

    /// <summary>
    /// Gets all methods that have unused local variables.
    /// </summary>
    /// <param name="entity">Injected solution entity.</param>
    /// <returns>Methods with at least one unused local variable.</returns>
    [BindableMethod]
    public IEnumerable<MethodEntity> GetMethodsWithUnusedVariables([InjectSpecificSource(typeof(SolutionEntity))] SolutionEntity entity)
    {
        return entity.Projects
            .SelectMany(p => p.Documents)
            .SelectMany(d => d.Classes)
            .SelectMany(c => c.Methods)
            .Where(m => m.UnusedVariableCount > 0);
    }

    /// <summary>
    /// Gets all unused parameters across the solution.
    /// </summary>
    /// <param name="entity">Injected solution entity.</param>
    /// <returns>All unused parameters with their containing method context.</returns>
    [BindableMethod]
    public IEnumerable<ParameterEntity> GetUnusedParameters([InjectSpecificSource(typeof(SolutionEntity))] SolutionEntity entity)
    {
        return entity.Projects
            .SelectMany(p => p.Documents)
            .SelectMany(d => d.Classes)
            .SelectMany(c => c.Methods)
            .SelectMany(m => m.Parameters)
            .Where(p => p.IsUsed == false);
    }

    /// <summary>
    /// Gets all unused local variables across the solution.
    /// </summary>
    /// <param name="entity">Injected solution entity.</param>
    /// <returns>All unused local variables with their containing method context.</returns>
    [BindableMethod]
    public IEnumerable<VariableEntity> GetUnusedVariables([InjectSpecificSource(typeof(SolutionEntity))] SolutionEntity entity)
    {
        return entity.Projects
            .SelectMany(p => p.Documents)
            .SelectMany(d => d.Classes)
            .SelectMany(c => c.Methods)
            .SelectMany(m => m.LocalVariables)
            .Where(v => !v.IsUsed);
    }

    /// <summary>
    /// Gets all unused class-level fields across the solution.
    /// </summary>
    /// <param name="entity">Injected solution entity.</param>
    /// <returns>All unused fields with their containing class context.</returns>
    [BindableMethod]
    public IEnumerable<FieldEntity> GetUnusedFields([InjectSpecificSource(typeof(SolutionEntity))] SolutionEntity entity)
    {
        return entity.Projects
            .SelectMany(p => p.Documents)
            .SelectMany(d => d.Classes)
            .SelectMany(c => c.Fields)
            .Where(f => f.IsUsed == false);
    }

    /// <summary>
    /// Gets all classes that have unused fields.
    /// </summary>
    /// <param name="entity">Injected solution entity.</param>
    /// <returns>Classes with at least one unused field.</returns>
    [BindableMethod]
    public IEnumerable<ClassEntity> GetClassesWithUnusedFields([InjectSpecificSource(typeof(SolutionEntity))] SolutionEntity entity)
    {
        return entity.Projects
            .SelectMany(p => p.Documents)
            .SelectMany(d => d.Classes)
            .Where(c => c.UnusedFieldCount > 0);
    }

    /// <summary>
    /// Gets all unused classes (classes with no references) across the solution.
    /// </summary>
    /// <param name="entity">Injected solution entity.</param>
    /// <returns>All unused classes.</returns>
    [BindableMethod]
    public IEnumerable<ClassEntity> GetUnusedClasses([InjectSpecificSource(typeof(SolutionEntity))] SolutionEntity entity)
    {
        return entity.Projects
            .SelectMany(p => p.Documents)
            .SelectMany(d => d.Classes)
            .Where(c => !c.IsUsed);
    }

    /// <summary>
    /// Gets all unused interfaces (interfaces with no references) across the solution.
    /// </summary>
    /// <param name="entity">Injected solution entity.</param>
    /// <returns>All unused interfaces.</returns>
    [BindableMethod]
    public IEnumerable<InterfaceEntity> GetUnusedInterfaces([InjectSpecificSource(typeof(SolutionEntity))] SolutionEntity entity)
    {
        return entity.Projects
            .SelectMany(p => p.Documents)
            .SelectMany(d => d.Interfaces)
            .Where(i => !i.IsUsed);
    }

    /// <summary>
    /// Gets all unused enums (enums with no references) across the solution.
    /// </summary>
    /// <param name="entity">Injected solution entity.</param>
    /// <returns>All unused enums.</returns>
    [BindableMethod]
    public IEnumerable<EnumEntity> GetUnusedEnums([InjectSpecificSource(typeof(SolutionEntity))] SolutionEntity entity)
    {
        return entity.Projects
            .SelectMany(p => p.Documents)
            .SelectMany(d => d.Enums)
            .Where(e => !e.IsUsed);
    }

    /// <summary>
    /// Gets all unused structs (structs with no references) across the solution.
    /// </summary>
    /// <param name="entity">Injected solution entity.</param>
    /// <returns>All unused structs.</returns>
    [BindableMethod]
    public IEnumerable<StructEntity> GetUnusedStructs([InjectSpecificSource(typeof(SolutionEntity))] SolutionEntity entity)
    {
        return entity.Projects
            .SelectMany(p => p.Documents)
            .SelectMany(d => d.Structs)
            .Where(s => !s.IsUsed);
    }
    
    internal async Task<IEnumerable<NugetPackageEntity>> GetNugetPackagesAsync(ProjectEntity project, bool withTransitivePackages)
    {
        var nugetPackageEntities = project.NugetPackageEntities;
        
        if (nugetPackageEntities != null)
            return nugetPackageEntities;
            
        project.NugetPackageEntities = await project.GetNugetPackagesAsync(project.Project, withTransitivePackages);
        
        return project.NugetPackageEntities;
    }
}