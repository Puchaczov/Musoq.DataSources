using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Os.Directories;

internal class DirectoriesSource : AsyncRowsSourceBase<DirectoryInfo>
{
    // ReSharper disable once InconsistentNaming
    private static readonly int MaxDegreeOfParallelism = Environment.ProcessorCount * 2;
    
    private const int ChunkSize = 2000;
    private readonly string _path;
    private readonly bool _recursive;

    public DirectoriesSource(string path, bool recursive, RuntimeContext communicator) 
        : base(communicator.EndWorkToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(communicator);
        
        _path = new DirectoryInfo(path).FullName;
        _recursive = recursive;
    }

    protected override async Task CollectChunksAsync(
        BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_path))
            return;

        var pendingResolvers = new List<string>(ChunkSize);
        
        await foreach (var dir in EnumerateDirectoriesAsync(_path, _recursive, cancellationToken))
        {
            pendingResolvers.Add(dir);

            if (pendingResolvers.Count < ChunkSize) continue;
            
            await ProcessResolverChunkAsync(pendingResolvers, chunkedSource, cancellationToken);
            pendingResolvers.Clear();
        }

        if (pendingResolvers.Count > 0)
            await ProcessResolverChunkAsync(pendingResolvers, chunkedSource, cancellationToken);
    }

    private static async Task ProcessResolverChunkAsync(
        List<string> dirs,
        BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource,
        CancellationToken cancellationToken)
    {
        var resolvers = new ConcurrentBag<IObjectResolver>();

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = MaxDegreeOfParallelism,
            CancellationToken = cancellationToken
        };

        await Task.Run(() =>
            Parallel.ForEach(dirs, options, dir =>
            {
                try
                {
                    var resolver = new EntityResolver<DirectoryInfo>(
                        new DirectoryInfo(dir),
                        SchemaDirectoriesHelper.DirectoriesNameToIndexMap,
                        SchemaDirectoriesHelper.DirectoriesIndexToMethodAccessMap);
                    resolvers.Add(resolver);
                }
                catch (Exception ex) when (ExpectedDirectoryException(ex))
                {
                    // ignored
                }
            }), cancellationToken);

        if (!resolvers.IsEmpty)
            chunkedSource.Add(resolvers.ToList(), cancellationToken);
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

            if (pendingDirs.Count <= 0 || pendingDirs.Count % 100 != 0) continue;
            
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private static bool ExpectedDirectoryException(Exception ex) =>
        ex is UnauthorizedAccessException or DirectoryNotFoundException or PathTooLongException;
}