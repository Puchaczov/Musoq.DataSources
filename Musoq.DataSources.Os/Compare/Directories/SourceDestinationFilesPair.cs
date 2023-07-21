using System.Collections;
using System.Collections.Generic;
using Musoq.DataSources.Os.Files;

namespace Musoq.DataSources.Os.Compare.Directories;

internal class SourceDestinationFilesPair : IReadOnlyList<ExtendedFileInfo>
{
    private readonly IReadOnlyList<ExtendedFileInfo> _files;

    public SourceDestinationFilesPair(IReadOnlyList<ExtendedFileInfo> files)
    {
        _files = files;
    }

    public ExtendedFileInfo Source => _files[0];

    public ExtendedFileInfo Destination => _files[1];

    public ExtendedFileInfo this[int index] => _files[index];

    public int Count => _files.Count;

    public IEnumerator<ExtendedFileInfo> GetEnumerator()
    {
        return _files.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}