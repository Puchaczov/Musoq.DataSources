using System;
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
    private IReadOnlyList<NugetPackageEntity>? _nugetPackageEntities;

    private readonly Project _project;
    private readonly INuGetPackageMetadataRetriever _nuGetPackageMetadataRetriever;
    private readonly CancellationToken _cancellationToken;

    private DocumentEntity[] _documents;
    private bool _wasLoaded;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectEntity"/> class.
    /// </summary>
    /// <param name="project">The project.</param>
    /// <param name="nuGetPackageMetadataRetriever">The NuGet package metadata retriever.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public ProjectEntity(Project project, INuGetPackageMetadataRetriever nuGetPackageMetadataRetriever, CancellationToken cancellationToken)
    {
        _project = project;
        _nuGetPackageMetadataRetriever = nuGetPackageMetadataRetriever;
        _cancellationToken = cancellationToken;
        _documents = [];
        _nugetPackageEntities = null;
    }

    /// <summary>
    /// Gets the project ID.
    /// </summary>
    public string Id => _project.Id.Id.ToString();

    /// <summary>
    /// Gets the file path of the project.
    /// </summary>
    public string? FilePath => _project.FilePath;

    /// <summary>
    /// Gets the output file path of the project.
    /// </summary>
    public string? OutputFilePath => _project.OutputFilePath;

    /// <summary>
    /// Gets the output reference file path of the project.
    /// </summary>
    public string? OutputRefFilePath => _project.OutputRefFilePath;

    /// <summary>
    /// Gets the default namespace of the project.
    /// </summary>
    public string? DefaultNamespace => _project.DefaultNamespace;

    /// <summary>
    /// Gets the language of the project.
    /// </summary>
    public string Language => _project.Language;

    /// <summary>
    /// Gets the assembly name of the project.
    /// </summary>
    public string AssemblyName => _project.AssemblyName;

    /// <summary>
    /// Gets the name of the project.
    /// </summary>
    public string Name => _project.Name;

    /// <summary>
    /// Gets a value indicating whether the project is a submission.
    /// </summary>
    public bool IsSubmission => _project.IsSubmission;

    /// <summary>
    /// Gets the version of the project.
    /// </summary>
    public string Version => _project.Version.ToString();

    /// <summary>
    /// Gets the documents in the project.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<DocumentEntity> Documents
    {
        get
        {
            if (_wasLoaded) return _documents;

            _documents = _project.Documents.Select(document => new DocumentEntity(document, _project.Solution)).ToArray();
            _wasLoaded = true;

            return _documents;
        }
    }
    
    /// <summary>
    /// Gets the references of the project.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<ProjectReferenceEntity> ProjectReferences => 
        _project.ProjectReferences.Select(reference => new ProjectReferenceEntity(reference, _project.Solution));
    
    /// <summary>
    /// Gets the library references of the project.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<LibraryReferenceEntity> LibraryReferences => 
        _project
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
    
    /// <summary>
    /// Gets the NuGet packages of the project.
    /// </summary>
    [BindablePropertyAsTable]
    public IReadOnlyList<NugetPackageEntity> NugetPackages
    {
        get
        {
            if (_nugetPackageEntities != null)
                return _nugetPackageEntities;
            
            var taskGetNugetPackages = Task.Run(async () => await GetNugetPackagesAsync(_project), _cancellationToken);
            taskGetNugetPackages.Wait();
            _nugetPackageEntities = taskGetNugetPackages.Result;
            
            return _nugetPackageEntities;
        }
    }

    private async Task<List<NugetPackageEntity>> GetNugetPackagesAsync(Project project)
    {   
        if (string.IsNullOrEmpty(project.FilePath))
            return [];

        try
        {
            var projectXml = XDocument.Load(project.FilePath);
            
            return await ExtractFromProjectMetadataAsync(projectXml, _nuGetPackageMetadataRetriever, _cancellationToken);
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
                null)
            ];
        }
    }

    internal static async Task<List<NugetPackageEntity>> ExtractFromProjectMetadataAsync(XDocument projectXml, INuGetPackageMetadataRetriever nuGetPackageMetadataRetriever, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var nugetPackages = new List<NugetPackageEntity>();
        var packageRefs = projectXml.Descendants("PackageReference");

        await Parallel.ForEachAsync(packageRefs, new ParallelOptions
        {
#if DEBUG
            MaxDegreeOfParallelism = 1,
#endif
            CancellationToken = cancellationToken
        }, async (packageRef, token) =>
        {
            var id = packageRef.Attribute("Include")?.Value ?? string.Empty;
            var version = packageRef.Attribute("Version")?.Value ?? string.Empty;

            await foreach (var metadata in nuGetPackageMetadataRetriever.GetMetadataAsync(id, version, token))
            {
                var requireLicenseAcceptanceString =
                    metadata.GetValueOrDefault(nameof(NugetPackageEntity.RequireLicenseAcceptance));
                var requireLicenseAcceptance = string.IsNullOrWhiteSpace(requireLicenseAcceptanceString)
                    ? "false"
                    : requireLicenseAcceptanceString;

                nugetPackages.Add(new NugetPackageEntity(
                    id,
                    version,
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
                    metadata.GetValueOrDefault(nameof(NugetPackageEntity.License))
                ));
            }
        });
        
        return nugetPackages;
    }
}