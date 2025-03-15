using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Musoq.DataSources.Roslyn.Components;

internal interface IFileSystem
{
    bool IsFileExists(string path);
    
    bool IsDirectoryExists(string path);
    
    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken);
    
    Task<Stream> CreateFileAsync(string tempFilePath);
    
    Task ExtractZipAsync(string tempFilePath, string packagePath, CancellationToken cancellationToken);
}