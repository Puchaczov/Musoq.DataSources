using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Musoq.Plugins.Attributes;
using System.Xml.Linq;
using Musoq.DataSources.Roslyn.Components.NuGet;

namespace Musoq.DataSources.Roslyn.Entities;

/// <summary>
/// Represents a project entity in the Roslyn data source.
/// </summary>
public class ProjectEntity
{
    private readonly INuGetPackageMetadataRetriever _nuGetPackageMetadataRetriever;

    private DocumentEntity[] _documents;
    private bool _wasLoaded;
    internal readonly Project Project;   
    internal readonly CancellationToken CancellationToken;
    internal IReadOnlyList<NugetPackageEntity>? NugetPackageEntities;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectEntity"/> class.
    /// </summary>
    /// <param name="project">The project.</param>
    /// <param name="nuGetPackageMetadataRetriever">The NuGet package metadata retriever.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public ProjectEntity(Project project, INuGetPackageMetadataRetriever nuGetPackageMetadataRetriever, CancellationToken cancellationToken)
    {
        Project = project;
        _nuGetPackageMetadataRetriever = nuGetPackageMetadataRetriever;
        CancellationToken = cancellationToken;
        _documents = [];
        NugetPackageEntities = null;
    }

    /// <summary>
    /// Gets the project ID.
    /// </summary>
    public string Id => Project.Id.Id.ToString();

    /// <summary>
    /// Gets the file path of the project.
    /// </summary>
    public string? FilePath => Project.FilePath;

    /// <summary>
    /// Gets the output file path of the project.
    /// </summary>
    public string? OutputFilePath => Project.OutputFilePath;

    /// <summary>
    /// Gets the output reference file path of the project.
    /// </summary>
    public string? OutputRefFilePath => Project.OutputRefFilePath;

    /// <summary>
    /// Gets the default namespace of the project.
    /// </summary>
    public string? DefaultNamespace => Project.DefaultNamespace;

    /// <summary>
    /// Gets the language of the project.
    /// </summary>
    public string Language => Project.Language;

    /// <summary>
    /// Gets the assembly name of the project.
    /// </summary>
    public string AssemblyName => Project.AssemblyName;

    /// <summary>
    /// Gets the name of the project.
    /// </summary>
    public string Name => Project.Name;

    /// <summary>
    /// Gets a value indicating whether the project is a submission.
    /// </summary>
    public bool IsSubmission => Project.IsSubmission;

    /// <summary>
    /// Gets the version of the project.
    /// </summary>
    public string Version => Project.Version.ToString();

    /// <summary>
    /// Gets the documents in the project.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<DocumentEntity> Documents
    {
        get
        {
            if (_wasLoaded) return _documents;

            _documents = Project.Documents.Select(document => new DocumentEntity(document, Project.Solution)).ToArray();
            _wasLoaded = true;

            return _documents;
        }
    }
    
    /// <summary>
    /// Gets the references of the project.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<ProjectReferenceEntity> ProjectReferences => 
        Project.ProjectReferences.Select(reference => new ProjectReferenceEntity(reference, Project.Solution));
    
    /// <summary>
    /// Gets the library references of the project.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<LibraryReferenceEntity> LibraryReferences => 
        Project
            .MetadataReferences
            .OfType<PortableExecutableReference>()
            .Where(f => f.Properties.Kind == MetadataImageKind.Assembly)
            .Select(reference => new LibraryReferenceEntity(reference));

    /// <summary>
    /// Gets the Project types.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<TypeEntity> Types
    {
        get
        {
            var types = new List<TypeEntity>();

            foreach (var document in Documents)
            {
                types.AddRange(document.Classes);
                types.AddRange(document.Interfaces);
                types.AddRange(document.Enums);
            }

            return types;
        }
    }

    internal async Task<List<NugetPackageEntity>> GetNugetPackagesAsync(Project project, bool withTransitivePackages)
    {   
        if (string.IsNullOrEmpty(project.FilePath))
            return [];

        try
        {
            var projectXml = XDocument.Load(project.FilePath);
            
            return await ExtractFromProjectMetadataAsync(projectXml, _nuGetPackageMetadataRetriever, withTransitivePackages, CancellationToken);
        }
        catch (Exception ex)
        {
            return [
                new NugetPackageEntity(
                "error",
                "error",
                $"error: {ex.Message}",
                null,
                null,
                null,
                null,
                false,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null)
            ];
        }
    }

    internal static async Task<List<NugetPackageEntity>> ExtractFromProjectMetadataAsync(XDocument projectXml, INuGetPackageMetadataRetriever nuGetPackageMetadataRetriever, bool withTransitivePackages, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var nugetPackages = new ConcurrentQueue<NugetPackageEntity>();
        var packageRefs = projectXml.Descendants("PackageReference");

        await Parallel.ForEachAsync(packageRefs, new ParallelOptions
        {
#if DEBUG
            MaxDegreeOfParallelism = 1,
#endif
            CancellationToken = cancellationToken
        }, async (packageRef, token) =>
        {
            token.ThrowIfCancellationRequested();
            
            var id = packageRef.Attribute("Include")?.Value ?? string.Empty;
            var version = packageRef.Attribute("Version")?.Value ?? string.Empty;
            var packagesToResolve = new BlockingCollection<(string PackageId, string Version, bool IsTransistive, uint Level)>();

            packagesToResolve.Add((id, version, false, 0), token);

            var processPackagesExtractionTask = Task.Run(async () => await ProcessPackagesExtractionAsync(nuGetPackageMetadataRetriever, packagesToResolve, nugetPackages, token), token);

            if (withTransitivePackages)
            {
                await foreach (var dependency in nuGetPackageMetadataRetriever.GetDependenciesAsync(id, version, token))
                {
                    packagesToResolve.Add((dependency.PackageId, dependency.VersionRange, true, dependency.Level), token);
                }
            }
            
            packagesToResolve.CompleteAdding();
            
            await processPackagesExtractionTask;
        });
        
        return nugetPackages.ToList();
    }

    private static async Task ProcessPackagesExtractionAsync(
        INuGetPackageMetadataRetriever nuGetPackageMetadataRetriever,
        BlockingCollection<(string PackageId, string Version, bool IsTransistive, uint Level)> packagesToResolve, 
        ConcurrentQueue<NugetPackageEntity> nugetPackages,
        CancellationToken token
    )
    {
        while ((!packagesToResolve.IsCompleted || packagesToResolve.Count > 0) && !token.IsCancellationRequested)
        {
            try
            {
                if (!packagesToResolve.TryTake(out var packageIdVersionPair, Timeout.Infinite, token)) 
                    continue;
                
                await foreach (var metadata in nuGetPackageMetadataRetriever.GetMetadataAsync(packageIdVersionPair.PackageId, packageIdVersionPair.Version, token))
                {
                    var requireLicenseAcceptanceString =
                        metadata.GetValueOrDefault(nameof(NugetPackageEntity.RequireLicenseAcceptance));
                    var requireLicenseAcceptance = string.IsNullOrWhiteSpace(requireLicenseAcceptanceString)
                        ? "false"
                        : requireLicenseAcceptanceString;

                    nugetPackages.Enqueue(new NugetPackageEntity(
                        packageIdVersionPair.PackageId, 
                        packageIdVersionPair.Version,
                        metadata.GetValueOrDefault(nameof(NugetPackageEntity.LicenseUrl)),
                        metadata.GetValueOrDefault(nameof(NugetPackageEntity.ProjectUrl)),
                        metadata.GetValueOrDefault(nameof(NugetPackageEntity.Title)),
                        metadata.GetValueOrDefault(nameof(NugetPackageEntity.Authors)),
                        metadata.GetValueOrDefault(nameof(NugetPackageEntity.Owners)),
                        Convert.ToBoolean(requireLicenseAcceptance),
                        metadata.GetValueOrDefault(nameof(NugetPackageEntity.Description)),
                        metadata.GetValueOrDefault(nameof(NugetPackageEntity.Summary)),
                        metadata.GetValueOrDefault(nameof(NugetPackageEntity.ReleaseNotes)),
                        metadata.GetValueOrDefault(nameof(NugetPackageEntity.Copyright)),
                        metadata.GetValueOrDefault(nameof(NugetPackageEntity.Language)),
                        metadata.GetValueOrDefault(nameof(NugetPackageEntity.Tags)),
                        metadata.GetValueOrDefault(nameof(NugetPackageEntity.LicenseContent)),
                        metadata.GetValueOrDefault(nameof(NugetPackageEntity.License)),
                        packageIdVersionPair.IsTransistive,
                        packageIdVersionPair.Level
                    ));
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}