using System;
using System.IO;

namespace Musoq.DataSources.Os.Runtime;

public sealed class SpecialFolderEntity
{
    public SpecialFolderEntity(string name, Environment.SpecialFolder folder)
    {
        Name = name;
        Path = GetPath(folder);
        Exists = Path.Length > 0 && Directory.Exists(Path);
    }

    public string Name { get; }
    public string Path { get; }
    public bool Exists { get; }

    private static string GetPath(Environment.SpecialFolder folder)
    {
        try
        {
            return Environment.GetFolderPath(folder);
        }
        catch (ArgumentException)
        {
            return string.Empty;
        }
    }
}
