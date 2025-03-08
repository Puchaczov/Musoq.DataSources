using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Musoq.DataSources.Roslyn.Components;

internal sealed class DefaultFileSystem : IFileSystem
{
    public bool IsFileExists(string path) => File.Exists(path);
    
    public bool IsDirectoryExists(string path) => Directory.Exists(path);

    public string ReadAllText(string path) => File.ReadAllText(path);
    
    public Task<Stream> CreateFileAsync(string tempFilePath)
    {
        return Task.FromResult<Stream>(new FileStream(tempFilePath, FileMode.Create, FileAccess.Write));
    }

    public Task ExtractZipAsync(string tempFilePath, string packagePath, CancellationToken cancellationToken)
    {
        var tempDirectory = Path.GetDirectoryName(tempFilePath);
        var packageDirectory = Path.GetDirectoryName(packagePath);
        
        if (tempDirectory is null || packageDirectory is null)
            return Task.CompletedTask;
        
        Directory.CreateDirectory(packageDirectory);
        File.Move(tempFilePath, packagePath);
        
        return Task.CompletedTask;
    }
}