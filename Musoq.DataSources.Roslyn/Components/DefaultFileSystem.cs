using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Musoq.DataSources.Roslyn.Components.NuGet.Helpers;

namespace Musoq.DataSources.Roslyn.Components;

internal sealed class DefaultFileSystem : IFileSystem
{
    public bool IsFileExists(string path) => File.Exists(path);
    
    public bool IsDirectoryExists(string path) => Directory.Exists(path);

    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken) => File.ReadAllTextAsync(path, cancellationToken);
    
    public Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        return File.WriteAllTextAsync(path, content, cancellationToken);
    }

    public Task<Stream> CreateFileAsync(string tempFilePath)
    {
        return Task.FromResult<Stream>(new FileStream(tempFilePath, FileMode.Create, FileAccess.Write));
    }

    public Task ExtractZipAsync(string tempFilePath, string packagePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var tempDirectory = Path.GetDirectoryName(tempFilePath);
        var packageDirectory = Path.GetDirectoryName(packagePath);
        
        if (tempDirectory is null || packageDirectory is null)
            return Task.CompletedTask;
        
        Directory.CreateDirectory(packageDirectory);
        File.Move(tempFilePath, packagePath);
        
        return Task.CompletedTask;
    }

    public IAsyncEnumerable<string> GetFilesAsync(string path, bool recursive, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.EnumerateFiles(path, "*", searchOption);

        return files.ToAsyncEnumerable();
    }

    public IEnumerable<string> GetFiles(string path, bool recursive, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.EnumerateFiles(path, "*", searchOption);

        return files;
    }
}