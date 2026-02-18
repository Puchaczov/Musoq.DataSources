using System.IO;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Os.Files;

internal class FilesSource(string path, bool useSubDirectories, RuntimeContext communicator)
    : EnumerateFilesSourceBase<FileEntity>(path, useSubDirectories, communicator)
{
    protected override EntityResolver<FileEntity> CreateBasedOnFile(FileInfo file, string rootDirectory)
    {
        return new EntityResolver<FileEntity>(
            new FileEntity(file, rootDirectory),
            SchemaFilesHelper.FilesNameToIndexMap,
            SchemaFilesHelper.FilesIndexToMethodAccessMap
        );
    }
}