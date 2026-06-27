using System.IO;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Os.Files;

internal class FilesSource(string path, bool useSubDirectories, SourceExecutionContext executionContext)
    : EnumerateFilesSourceBase<FileEntity>(path, useSubDirectories, executionContext)
{
    protected override FileEntity CreateBasedOnFile(FileInfo file, string rootDirectory)
    {
        return new FileEntity(file, rootDirectory);
    }
}
