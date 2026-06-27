using System.Collections.Generic;
using System.IO;
using System.Linq;
using Musoq.DataSources.Os.Directories;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Os.Tests.Utils;

internal class TestDirectoriesSource(string path, bool recursive, SourceExecutionContext context)
    : DirectoriesSource(path, recursive, context)
{
    public IReadOnlyList<DirectoryInfo> GetDirectories()
    {
        return Chunks.SelectMany(chunk => chunk).ToArray();
    }
}
