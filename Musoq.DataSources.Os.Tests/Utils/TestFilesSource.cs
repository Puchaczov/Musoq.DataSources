using System.Collections.Generic;
using System.Linq;
using Musoq.DataSources.Os.Files;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Os.Tests.Utils;

internal class TestFilesSource(string path, bool useSubDirectories, SourceExecutionContext context)
    : FilesSource(path, useSubDirectories, context)
{
    public IReadOnlyList<FileEntity> GetFiles()
    {
        return Chunks.SelectMany(chunk => chunk).ToArray();
    }
}
