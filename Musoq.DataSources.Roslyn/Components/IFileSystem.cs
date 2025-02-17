namespace Musoq.DataSources.Roslyn.Components;

internal interface IFileSystem
{
    bool IsFileExists(string path);
    
    bool IsDirectoryExists(string path);
    
    string ReadAllText(string path);
}