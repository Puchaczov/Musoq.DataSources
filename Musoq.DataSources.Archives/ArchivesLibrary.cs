using System.Diagnostics.CodeAnalysis;
using System.IO;
using Musoq.Plugins;
using Musoq.Plugins.Attributes;
using SharpCompress.Readers;

namespace Musoq.DataSources.Archives;

/// <summary>
///     Archives helper methods
/// </summary>
[BindableClass]
[SuppressMessage("ReSharper", "UnusedMember.Global")]
public class ArchivesLibrary : LibraryBase
{
    /// <summary>
    ///     Gets the content of the entry as byte array.
    /// </summary>
    /// <param name="source" injectedByRuntime="true">the archive entry</param>
    /// <returns>Content of a file</returns>
    [BindableMethod]
    public byte[] GetContent([InjectSpecificSource(typeof(EntryWrapper))] EntryWrapper source)
    {
        using var stream = InternalGetStreamContent(source);

        return stream.ToArray();
    }

    /// <summary>
    ///     Gets the content of the entry as string.
    /// </summary>
    /// <param name="source">the archive entry</param>
    /// <returns></returns>
    [BindableMethod]
    public string GetTextContent([InjectSpecificSource(typeof(EntryWrapper))] EntryWrapper source)
    {
        using var stream = InternalGetStreamContent(source);
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }

    /// <summary>
    ///     Gets the content of the entry as stream.
    /// </summary>
    /// <param name="source" injectedByRuntime="true">the archive entry</param>
    /// <returns>Stream of a file</returns>
    [BindableMethod]
    public Stream GetStreamContent([InjectSpecificSource(typeof(EntryWrapper))] EntryWrapper source)
    {
        return InternalGetStreamContent(source);
    }

    private static MemoryStream InternalGetStreamContent(EntryWrapper source)
    {
        using var fileStream = File.OpenRead(source.PathToArchive);
        using var reader = ReaderFactory.Open(fileStream, new ReaderOptions
        {
            LeaveStreamOpen = true
        });

        var currentIndex = 0;
        while (reader.MoveToNextEntry())
        {
            if (currentIndex == source.Index)
                break;

            currentIndex++;
        }

        using var stream = reader.OpenEntryStream();
        var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);

        memoryStream.Position = 0;

        return memoryStream;
    }
}