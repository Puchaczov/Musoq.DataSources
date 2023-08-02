using System.Diagnostics.CodeAnalysis;
using System.IO;
using Musoq.Plugins;
using Musoq.Plugins.Attributes;

namespace Musoq.DataSources.Archives;

/// <summary>
/// Archives helper methods
/// </summary>
[BindableClass]
[SuppressMessage("ReSharper", "UnusedMember.Global")]
public class ArchivesLibrary : LibraryBase
{
    /// <summary>
    /// Gets the content of the entry as byte array.
    /// </summary>
    /// <param name="source" injectedByRuntime="true"></param>
    /// <returns>Content of a file</returns>
    [BindableMethod]
    public byte[] GetContent([InjectSpecificSource(typeof(EntryWrapper))] EntryWrapper source)
    {
        using var stream = source.Reader.OpenEntryStream();
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }
    
    /// <summary>
    /// Gets the content of the entry as stream.
    /// </summary>
    /// <param name="source" injectedByRuntime="true"></param>
    /// <returns>Stream of a file</returns>
    [BindableMethod]
    public Stream GetContentStream([InjectSpecificSource(typeof(EntryWrapper))] EntryWrapper source)
    {
        using var stream = source.Reader.OpenEntryStream();
        var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        
        memoryStream.Position = 0;
        
        return memoryStream;
    }
}