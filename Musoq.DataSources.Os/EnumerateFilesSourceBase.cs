using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Os;

internal abstract class EnumerateFilesSourceBase<TEntity>(
    string path,
    bool useSubDirectories,
    RuntimeContext communicator)
    : AsyncRowsSourceBase<TEntity>(communicator.EndWorkToken)
{
    private readonly DirectorySourceSearchOptions[] _source =
    [
        new(new DirectoryInfo(path).FullName, useSubDirectories)
    ];

    protected override async Task CollectChunksAsync(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource, CancellationToken cancellationToken)
    {
        await Parallel.ForEachAsync(
            _source, 
            cancellationToken,
            (source, token) => 
            {
                var sources = new Stack<DirectorySourceSearchOptions>();

                if (!Directory.Exists(source.Path))
                    return ValueTask.CompletedTask;
                    
                sources.Push(source);

                while (sources.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                        
                    var currentSource = sources.Pop();
                    var dir = new DirectoryInfo(currentSource.Path);
                    var dirFiles = new List<EntityResolver<TEntity>>();

                    try
                    {
                        foreach (var file in GetFiles(dir))
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                                
                            ProcessFile(file, source, dirFiles);
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        continue;
                    }

                    if (dirFiles.Count > 0) 
                        chunkedSource.Add(dirFiles, token);

                    if (currentSource.WithSubDirectories)
                        foreach (var subDir in dir.GetDirectories())
                            sources.Push(new DirectorySourceSearchOptions(subDir.FullName, currentSource.WithSubDirectories));
                }
                
                return ValueTask.CompletedTask;
            });
    }

    protected virtual FileInfo[] GetFiles(DirectoryInfo directoryInfo) => directoryInfo.GetFiles();

    protected virtual void ProcessFile(FileInfo file, DirectorySourceSearchOptions source, List<EntityResolver<TEntity>> dirFiles)
    {
        var entity = CreateBasedOnFile(file, source.Path);
        
        if (entity != null)
            dirFiles.Add(entity);
    }

    protected virtual EntityResolver<TEntity>? CreateBasedOnFile(FileInfo file, string rootDirectory) => null;
}