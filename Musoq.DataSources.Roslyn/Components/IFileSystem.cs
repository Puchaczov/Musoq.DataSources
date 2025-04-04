using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Musoq.DataSources.Roslyn.Components;

internal interface IFileSystem
{
    bool IsFileExists(string path);
    
    bool IsDirectoryExists(string path);
    
    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken);
    
    Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken);
    
    Task<Stream> CreateFileAsync(string tempFilePath);
    
    Task ExtractZipAsync(string tempFilePath, string packagePath, CancellationToken cancellationToken);
    
    IAsyncEnumerable<string> GetFilesAsync(string path, bool recursive, CancellationToken cancellationToken);
    
    IEnumerable<string> GetFiles(string path, bool recursive, CancellationToken cancellationToken);
    
    static string Combine(params string[] paths) => Path.Combine(paths);
    
    static string GetFileName(string path) => Path.GetFileName(path);
    
    static bool DirectoryExists(string directory) => Directory.Exists(directory);
    
    static void CreateDirectory(string directory) => Directory.CreateDirectory(directory);
    
    static string GetExtension(string filePath) => Path.GetExtension(filePath);
}