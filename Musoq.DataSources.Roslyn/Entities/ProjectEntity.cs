using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Musoq.Plugins.Attributes;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using System.Xml.Linq;
using Musoq.DataSources.Roslyn.Components;
using Musoq.DataSources.Roslyn.Components.NuGet;

namespace Musoq.DataSources.Roslyn.Entities;

/// <summary>
/// Represents a project entity in the Roslyn data source.
/// </summary>
public class ProjectEntity
{
    /// <summary>
    /// Maps column names to their respective indices.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, int> NameToIndexMap;

    /// <summary>
    /// Maps column indices to functions that access the corresponding properties of a <see cref="ProjectEntity"/>.
    /// </summary>
    public static readonly IReadOnlyDictionary<int, Func<ProjectEntity, object?>> IndexToObjectAccessMap;

    /// <summary>
    /// Defines the schema columns for the <see cref="ProjectEntity"/>.
    /// </summary>
    public static readonly ISchemaColumn[] Columns =
    [
        new SchemaColumn(nameof(Id), 0, typeof(string)),
        new SchemaColumn(nameof(FilePath), 1, typeof(string)),
        new SchemaColumn(nameof(OutputFilePath), 2, typeof(string)),
        new SchemaColumn(nameof(OutputRefFilePath), 3, typeof(string)),
        new SchemaColumn(nameof(DefaultNamespace), 4, typeof(string)),
        new SchemaColumn(nameof(Language), 5, typeof(string)),
        new SchemaColumn(nameof(AssemblyName), 6, typeof(string)),
        new SchemaColumn(nameof(Name), 7, typeof(string)),
        new SchemaColumn(nameof(IsSubmission), 8, typeof(bool)),
        new SchemaColumn(nameof(Version), 9, typeof(string)),
        new SchemaColumn(nameof(Documents), 10, typeof(DocumentEntity[])),
        new SchemaColumn(nameof(ProjectReferences), 11, typeof(ProjectReferenceEntity[])),
        new SchemaColumn(nameof(LibraryReferences), 12, typeof(LibraryReferenceEntity[])),
        new SchemaColumn(nameof(NugetPackages), 13, typeof(NugetPackageEntity[])),
        new SchemaColumn(nameof(Types), 14, typeof(TypeEntity[]))
    ];

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
        NugetPackages = GetNugetPackagesAsync(project).Result;
    }

    /// <summary>
    /// Initializes static members of the <see cref="ProjectEntity"/> class.
    /// </summary>
    static ProjectEntity()
    {
        NameToIndexMap = new Dictionary<string, int>
        {
            {nameof(Id), 0},
            {nameof(FilePath), 1},
            {nameof(OutputFilePath), 2},
            {nameof(OutputRefFilePath), 3},
            {nameof(DefaultNamespace), 4},
            {nameof(Language), 5},
            {nameof(AssemblyName), 6},
            {nameof(Name), 7},
            {nameof(IsSubmission), 8},
            {nameof(Version), 9},
            {nameof(Documents), 10},
            {nameof(ProjectReferences), 11},
            {nameof(LibraryReferences), 12},
            {nameof(NugetPackages), 13},
            {nameof(Types), 14}
        };

        IndexToObjectAccessMap = new Dictionary<int, Func<ProjectEntity, object?>>
        {
            {0, entity => entity.Id},
            {1, entity => entity.FilePath},
            {2, entity => entity.OutputFilePath},
            {3, entity => entity.OutputRefFilePath},
            {4, entity => entity.DefaultNamespace},
            {5, entity => entity.Language},
            {6, entity => entity.AssemblyName},
            {7, entity => entity.Name},
            {8, entity => entity.IsSubmission},
            {9, entity => entity.Version},
            {10, entity => entity.Documents},
            {11, entity => entity.ProjectReferences},
            {12, entity => entity.LibraryReferences},
            {13, entity => entity.NugetPackages},
            {14, entity => entity.Types}
        };
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
    public IReadOnlyList<NugetPackageEntity> NugetPackages { get; }

    private async Task<List<NugetPackageEntity>> GetNugetPackagesAsync(Project project)
    {
        var nugetPackages = new List<NugetPackageEntity>();
        if (string.IsNullOrEmpty(project.FilePath))
            return nugetPackages;

        try
        {
            var projectXml = XDocument.Load(project.FilePath);
            var packageRefs = projectXml.Descendants("PackageReference");

            foreach (var packageRef in packageRefs)
            {
                var id = packageRef.Attribute("Include")?.Value ?? string.Empty;
                var version = packageRef.Attribute("Version")?.Value ?? string.Empty;
                var metadata = await _nuGetPackageMetadataRetriever.GetMetadataAsync(id, version, _cancellationToken);

                var requireLicenseAcceptanceString = metadata.GetValueOrDefault(nameof(NugetPackageEntity.RequireLicenseAcceptance));
                var requireLicenseAcceptance = string.IsNullOrWhiteSpace(requireLicenseAcceptanceString) ? "false" : requireLicenseAcceptanceString;
                
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
                    metadata.GetValueOrDefault(nameof(NugetPackageEntity.LicenseContent))
                ));
            }
        }
        catch (Exception ex)
        {
            nugetPackages.Add(new NugetPackageEntity(
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
                null));
            return nugetPackages;
        }

        return nugetPackages;
    }
}