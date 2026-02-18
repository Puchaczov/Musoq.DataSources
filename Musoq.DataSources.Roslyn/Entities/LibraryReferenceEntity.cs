using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis;

namespace Musoq.DataSources.Roslyn.Entities;

/// <summary>
///     Represents a project reference entity in the Roslyn data source.
/// </summary>
public class LibraryReferenceEntity
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="LibraryReferenceEntity" /> class.
    /// </summary>
    /// <param name="reference">The project reference.</param>
    public LibraryReferenceEntity(PortableExecutableReference reference)
    {
        if (reference.FilePath is null)
            return;

        using var stream = File.OpenRead(reference.FilePath);
        using var peReader = new PEReader(stream);

        var metadataReader = peReader.GetMetadataReader();
        var assemblyDefinition = metadataReader.GetAssemblyDefinition();
        var assemblyName = metadataReader.GetString(assemblyDefinition.Name);
        var version = assemblyDefinition.Version.ToString();
        var culture = metadataReader.GetString(assemblyDefinition.Culture);
        var location = reference.FilePath;

        Name = assemblyName;
        Version = version;
        Culture = culture;
        Location = location;
    }

    /// <summary>
    ///     Gets the name of the library reference.
    /// </summary>
    public string? Name { get; }

    /// <summary>
    ///     Gets the version of the library reference.
    /// </summary>
    public string? Version { get; }

    /// <summary>
    ///     Gets the culture of the library reference.
    /// </summary>
    public string? Culture { get; }

    /// <summary>
    ///     Gets the location of the library reference.
    /// </summary>
    public string? Location { get; }
}