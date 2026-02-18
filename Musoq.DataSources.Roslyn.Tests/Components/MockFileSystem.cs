using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using Musoq.DataSources.Roslyn.Components;

namespace Musoq.DataSources.Roslyn.Tests.Components;

/// <summary>
///     Mock implementation of IFileSystem for testing purposes.
/// </summary>
public class MockFileSystem : IFileSystem
{
    // Track directories
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);

    // Track subscribers to file changes for testing
    private readonly ConcurrentDictionary<string, List<Action<string>>> _fileWatchers = new();

    public bool IsFileExists(string path)
    {
        return _files.ContainsKey(NormalizePath(path));
    }

    public bool IsDirectoryExists(string path)
    {
        return _directories.Contains(NormalizePath(path));
    }

    public IEnumerable<string> GetFiles(string path, bool recursive, CancellationToken cancellationToken)
    {
        var normalizedPath = NormalizePath(path);
        return _files.Keys
            .Where(filePath => filePath.StartsWith(normalizedPath, StringComparison.OrdinalIgnoreCase) &&
                               (recursive || (Path.GetDirectoryName(filePath)
                                   ?.Equals(normalizedPath, StringComparison.OrdinalIgnoreCase) ?? false)))
            .ToArray();
    }

    public async IAsyncEnumerable<string> GetFilesAsync(string path, bool recursive,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var file in GetFiles(path, recursive, cancellationToken)) yield return file;
    }

    public async Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken)
    {
        var normalizedPath = NormalizePath(path);
        if (_files.TryGetValue(normalizedPath, out var bytes)) return Encoding.UTF8.GetString(bytes);

        throw new FileNotFoundException($"File not found: {path}", path);
    }

    public string ReadAllText(string path, CancellationToken cancellationToken)
    {
        var normalizedPath = NormalizePath(path);
        if (_files.TryGetValue(normalizedPath, out var bytes)) return Encoding.UTF8.GetString(bytes);

        throw new FileNotFoundException($"File not found: {path}", path);
    }

    public Stream OpenRead(string path)
    {
        var normalizedPath = NormalizePath(path);
        if (_files.TryGetValue(normalizedPath, out var bytes)) return new MemoryStream(bytes);

        throw new FileNotFoundException($"File not found: {path}", path);
    }

    public Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken)
    {
        var normalizedPath = NormalizePath(path);
        var bytes = Encoding.UTF8.GetBytes(content);
        _files[normalizedPath] = bytes;


        var directory = Path.GetDirectoryName(normalizedPath);
        if (directory != null) _directories.Add(directory);


        if (_fileWatchers.TryGetValue(normalizedPath, out var subscribers))
            foreach (var subscriber in subscribers)
                subscriber(normalizedPath);

        return Task.CompletedTask;
    }

    public void WriteAllText(string path, string content, CancellationToken cancellationToken)
    {
        var normalizedPath = NormalizePath(path);
        var bytes = Encoding.UTF8.GetBytes(content);
        _files[normalizedPath] = bytes;

        var directory = Path.GetDirectoryName(normalizedPath);
        if (directory != null) _directories.Add(directory);

        if (_fileWatchers.TryGetValue(normalizedPath, out var subscribers))
            foreach (var subscriber in subscribers)
                subscriber(normalizedPath);
    }

    public Task<Stream> CreateFileAsync(string tempFilePath)
    {
        var normalizedPath = NormalizePath(tempFilePath);
        var stream = new MemoryStream();


        return Task.FromResult<Stream>(new DelegatingStream(stream, bytes =>
        {
            _files[normalizedPath] = bytes;


            var directory = Path.GetDirectoryName(normalizedPath);
            if (directory != null) _directories.Add(directory);


            if (_fileWatchers.TryGetValue(normalizedPath, out var subscribers))
                foreach (var subscriber in subscribers)
                    subscriber(normalizedPath);
        }));
    }

    public Task ExtractZipAsync(string tempFilePath, string packagePath, CancellationToken cancellationToken)
    {
        var dummyFilePath = Path.Combine(packagePath, "extracted_file.txt");
        var normalizedPath = NormalizePath(dummyFilePath);
        _files[normalizedPath] = Encoding.UTF8.GetBytes("Extracted file content");

        var directory = Path.GetDirectoryName(normalizedPath);
        if (directory != null) _directories.Add(directory);

        return Task.CompletedTask;
    }

    public void CreateDirectory(string path)
    {
        if (!string.IsNullOrEmpty(path)) _directories.Add(NormalizePath(path));
    }

    public void DeleteFile(string path)
    {
        var normalizedPath = NormalizePath(path);
        _files.TryRemove(normalizedPath, out _);


        if (_fileWatchers.TryGetValue(normalizedPath, out var subscribers))
            foreach (var subscriber in subscribers)
                subscriber(normalizedPath);
    }

    // Helper methods for testing

    public void WatchFile(string path, Action<string> callback)
    {
        var normalizedPath = NormalizePath(path);
        if (!_fileWatchers.TryGetValue(normalizedPath, out var subscribers))
        {
            subscribers = [];
            _fileWatchers[normalizedPath] = subscribers;
        }

        subscribers.Add(callback);
    }

    public void SimulateFileCreated(string path, string content)
    {
        var normalizedPath = NormalizePath(path);
        var bytes = Encoding.UTF8.GetBytes(content);
        _files[normalizedPath] = bytes;


        var directory = Path.GetDirectoryName(normalizedPath);
        if (directory != null) _directories.Add(directory);


        if (_fileWatchers.TryGetValue(normalizedPath, out var subscribers))
            foreach (var subscriber in subscribers)
                subscriber(normalizedPath);


        Console.WriteLine($"MockFileSystem: Created file at {normalizedPath} with content length {content.Length}");
    }

    public void SimulateFileDeleted(string path)
    {
        DeleteFile(NormalizePath(path));
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }

    // Helper stream class to capture data when a stream is closed
    private class DelegatingStream(MemoryStream innerStream, Action<byte[]> onClose) : MemoryStream
    {
        public override bool CanRead => innerStream.CanRead;
        public override bool CanSeek => innerStream.CanSeek;
        public override bool CanWrite => innerStream.CanWrite;
        public override long Length => innerStream.Length;

        public override long Position
        {
            get => innerStream.Position;
            set => innerStream.Position = value;
        }

        public override async ValueTask DisposeAsync()
        {
            await innerStream.FlushAsync();
            onClose(innerStream.ToArray());
            await innerStream.DisposeAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                innerStream.Flush();
                onClose(innerStream.ToArray());
                innerStream.Dispose();
            }

            base.Dispose(disposing);
        }

        // Delegate all operations to the inner stream
        public override int Read(byte[] buffer, int offset, int count)
        {
            return innerStream.Read(buffer, offset, count);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return innerStream.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return innerStream.ReadAsync(buffer, cancellationToken);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            innerStream.Write(buffer, offset, count);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return innerStream.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return innerStream.WriteAsync(buffer, cancellationToken);
        }

        public override void Flush()
        {
            innerStream.Flush();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return innerStream.FlushAsync(cancellationToken);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return innerStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            innerStream.SetLength(value);
        }
    }
}