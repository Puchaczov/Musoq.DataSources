using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Os.Directories;

internal class DirectoriesSource : AsyncRowsSourceBase<DirectoryInfo>
{
    private const string DirectoriesSourceName = "directories";
    private const int ChunkSize = 2000;
    private readonly SourceExecutionContext _executionContext;
    private readonly string _path;
    private readonly bool _recursive;

    public DirectoriesSource(string path, bool recursive, SourceExecutionContext executionContext)
        : base(executionContext.EndWorkToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(executionContext);

        _path = new DirectoryInfo(path).FullName;
        _recursive = recursive;
        _executionContext = executionContext;
    }

    protected override async Task CollectChunksAsync(
        IChunkWriter<DirectoryInfo> writer,
        CancellationToken cancellationToken)
    {
        _executionContext.ReportDataSourceBegin(DirectoriesSourceName);
        long totalRowsProcessed = 0;

        try
        {
            if (!Directory.Exists(_path))
                return;

            var chunk = new List<DirectoryInfo>(ChunkSize);

            await foreach (var dir in EnumerateDirectoriesAsync(_path, _recursive, cancellationToken))
            {
                chunk.Add(new DirectoryInfo(dir));

                if (chunk.Count < ChunkSize)
                    continue;

                writer.Write(chunk);
                totalRowsProcessed += chunk.Count;
                chunk = [];
            }

            if (chunk.Count > 0)
            {
                writer.Write(chunk);
                totalRowsProcessed += chunk.Count;
            }
        }
        finally
        {
            _executionContext.ReportDataSourceEnd(DirectoriesSourceName, totalRowsProcessed);
        }
    }

    private static async IAsyncEnumerable<string> EnumerateDirectoriesAsync(
        string rootPath,
        bool recursive,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var pendingDirs = new Queue<string>();
        pendingDirs.Enqueue(rootPath);

        while (pendingDirs.Count > 0)
        {
            var currentDir = pendingDirs.Dequeue();
            string[] subDirs;

            try
            {
                subDirs = Directory.GetDirectories(currentDir);
            }
            catch (Exception ex) when (ExpectedDirectoryException(ex))
            {
                continue;
            }

            foreach (var dir in subDirs)
            {
                yield return dir;

                if (recursive)
                    pendingDirs.Enqueue(dir);
            }

            if (pendingDirs.Count <= 0 || pendingDirs.Count % 100 != 0)
                continue;

            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private static bool ExpectedDirectoryException(Exception ex)
    {
        return ex is UnauthorizedAccessException or DirectoryNotFoundException or PathTooLongException;
    }
}
