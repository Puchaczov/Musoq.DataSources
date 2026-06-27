using System.Diagnostics;
using System.IO;
using System.Reflection;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Os.Dlls;

internal class DllSource(string path, bool useSubDirectories, SourceExecutionContext executionContext)
    : EnumerateFilesSourceBase<DllInfo>(path, useSubDirectories, executionContext)
{
    protected override DllInfo? CreateBasedOnFile(FileInfo file, string rootDirectory)
    {
        Assembly? asm;
        try
        {
            asm = Assembly.LoadFrom(file.FullName);
        }
        catch
        {
            asm = null;
        }

        if (asm == null)
            return null;

        var version = FileVersionInfo.GetVersionInfo(asm.Location);
        return new DllInfo
        {
            FileInfo = file,
            Assembly = asm,
            Version = version
        };
    }

    protected override FileInfo[] GetFiles(DirectoryInfo directoryInfo)
    {
        return directoryInfo.GetFiles("*.dll");
    }
}
