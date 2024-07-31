namespace Musoq.DataSources.Os.Metadata;

/// <summary>
/// Metadata entity
/// </summary>
public class MetadataEntity
{
    /// <summary>
    /// Initializes a new instance of metadata entity
    /// </summary>
    /// <param name="fullName">The full path</param>
    /// <param name="directoryName">The directory name</param>
    /// <param name="tagName">The tag name</param>
    /// <param name="description">The description</param>
    public MetadataEntity(string fullName, string directoryName, string tagName, string? description)
    {
        DirectoryName = directoryName;
        TagName = tagName;
        Description = description;
        FullName = fullName;
    }
    
    /// <summary>
    /// Full path to the file
    /// </summary>
    public string FullName { get; }

    /// <summary>
    /// Directory name
    /// </summary>
    public string DirectoryName { get; }
    
    /// <summary>
    /// Tag name
    /// </summary>
    public string TagName { get; }
    
    /// <summary>
    /// Description
    /// </summary>
    public string? Description { get; }
}