using Musoq.Schema.DataSources;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Musoq.Schema;

namespace Musoq.DataSources.Os.Dlls;

internal class DllSource(string path, bool useSubDirectories, RuntimeContext communicator)
    : EnumerateFilesSourceBase<DllInfo>(path, useSubDirectories, communicator)
{
    protected override EntityResolver<DllInfo>? CreateBasedOnFile(FileInfo file, string rootDirectory)
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
        return new EntityResolver<DllInfo>(new DllInfo
        {
            FileInfo = file,
            Assembly = asm,
            Version = version
        }, DllInfosHelper.DllInfosNameToIndexMap, DllInfosHelper.DllInfosIndexToMethodAccessMap);
    }

    protected override FileInfo[] GetFiles(DirectoryInfo directoryInfo)
    {
        return directoryInfo.GetFiles("*.dll");
    }
}