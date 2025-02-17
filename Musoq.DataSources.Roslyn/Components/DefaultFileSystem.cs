using System.IO;

namespace Musoq.DataSources.Roslyn.Components;

internal sealed class DefaultFileSystem : IFileSystem
{
    public bool IsFileExists(string path) => File.Exists(path);
    
    public bool IsDirectoryExists(string path) => Directory.Exists(path);

    public string ReadAllText(string path) => File.ReadAllText(path);
}