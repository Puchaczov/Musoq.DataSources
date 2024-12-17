using System.Collections;
using System.Collections.Generic;
using Musoq.DataSources.Os.Files;

namespace Musoq.DataSources.Os.Compare.Directories;

internal class SourceDestinationFilesPair(IReadOnlyList<FileEntity> files) : IReadOnlyList<FileEntity>
{
    public FileEntity? Source => files.Count > 0 ? files[0] : null;

    public FileEntity? Destination => files.Count > 1 ? files[1] : null;

    public FileEntity this[int index] => files[index];

    public int Count => files.Count;

    public IEnumerator<FileEntity> GetEnumerator()
    {
        return files.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}