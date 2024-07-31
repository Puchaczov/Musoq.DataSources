using System.IO;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Os.Files;

internal class FilesSource : EnumerateFilesSourceBase<ExtendedFileInfo>
{
    public FilesSource(string path, bool useSubDirectories, RuntimeContext communicator) 
        : base(path, useSubDirectories, communicator)
    {
    }

    public FilesSource(IReadOnlyTable table, RuntimeContext runtimeContext)
        : base(table, runtimeContext) 
    {
    }

    protected override EntityResolver<ExtendedFileInfo> CreateBasedOnFile(FileInfo file, string rootDirectory)
    {
        return new EntityResolver<ExtendedFileInfo>(new ExtendedFileInfo(file, rootDirectory), SchemaFilesHelper.FilesNameToIndexMap,
            SchemaFilesHelper.FilesIndexToMethodAccessMap);
    }
}