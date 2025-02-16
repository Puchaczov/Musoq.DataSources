namespace Musoq.DataSources.Roslyn.Components;

internal interface IFileSystem
{
    bool Exists(string path);
    string ReadAllText(string path);
}