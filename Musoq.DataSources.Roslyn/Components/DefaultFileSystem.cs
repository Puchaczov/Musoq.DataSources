using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Musoq.DataSources.Roslyn.Components.NuGet.Helpers;

namespace Musoq.DataSources.Roslyn.Components;

internal sealed class DefaultFileSystem : IFileSystem
{
    public bool IsFileExists(string path) => File.Exists(path);
    
    public bool IsDirectoryExists(string path) => Directory.Exists(path);

    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken) => File.ReadAllTextAsync(path, cancellationToken);
    
    public string ReadAllText(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        return File.ReadAllText(path);
    }

    public Stream OpenRead(string path)
    {
        return File.OpenRead(path);
    }

    public Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken) => File.WriteAllTextAsync(path, content, cancellationToken);
    
    public void WriteAllText(string path, string content, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        File.WriteAllText(path, content);
    }

    public Task<Stream> CreateFileAsync(string tempFilePath)
    {
        return Task.FromResult<Stream>(new FileStream(tempFilePath, FileMode.Create, FileAccess.Write));
    }

    public Task ExtractZipAsync(string tempFilePath, string directoryPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var tempDirectory = Path.GetDirectoryName(tempFilePath);
        
        if (tempDirectory is null)
            return Task.CompletedTask;
        
        Directory.CreateDirectory(directoryPath);
        
        ZipFile.ExtractToDirectory(tempFilePath, directoryPath);
        
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