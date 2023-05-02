using System.IO;
using Musoq.Plugins;
using Musoq.Plugins.Attributes;

namespace Musoq.DataSources.Archives;

public class ArchivesLibrary : LibraryBase
{
    public byte[] GetContent([InjectSource] EntryWrapper source)
    {
        using var stream = source.Reader.OpenEntryStream();
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }
    
    public Stream GetContentStream([InjectSource] EntryWrapper source)
    {
        using var stream = source.Reader.OpenEntryStream();
        var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        
        memoryStream.Position = 0;
        
        return memoryStream;
    }
}