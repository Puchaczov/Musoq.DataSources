﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Os;

internal abstract class EnumerateFilesSourceBase<TEntity> : RowSourceBase<TEntity>
{
    private readonly RuntimeContext _communicator;
    private readonly DirectorySourceSearchOptions[] _source;

    protected EnumerateFilesSourceBase(string path, bool useSubDirectories, RuntimeContext communicator)
    {
        _communicator = communicator;
        _source =
        [
            new DirectorySourceSearchOptions(new DirectoryInfo(path).FullName, useSubDirectories)
        ];
    }

    protected EnumerateFilesSourceBase(IReadOnlyTable table, RuntimeContext context)
    {
        _communicator = context;
        _source = table.Rows.Select(row => new DirectorySourceSearchOptions(new DirectoryInfo((string) row[0]).FullName, (bool) row[1])).ToArray();
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        Parallel.ForEach(
            _source, 
            new ParallelOptions 
            { 
                MaxDegreeOfParallelism = Environment.ProcessorCount 
            }, 
            source => 
            {
                try
                {

                    var sources = new Stack<DirectorySourceSearchOptions>();

                    if (!Directory.Exists(source.Path))
                        return;

                    var endWorkToken = _communicator.EndWorkToken;

                    sources.Push(source);

                    while (sources.Count > 0)
                    {
                        var currentSource = sources.Pop();
                        var dir = new DirectoryInfo(currentSource.Path);
                        var dirFiles = new List<EntityResolver<TEntity>>();

                        try
                        {
                            foreach (var file in GetFiles(dir))
                            {
                                ProcessFile(file, source, dirFiles);
                            }
                        }
                        catch (UnauthorizedAccessException)
                        {
                            continue;
                        }

                        chunkedSource.Add(dirFiles, endWorkToken);

                        if (currentSource.WithSubDirectories)
                            foreach (var subDir in dir.GetDirectories())
                                sources.Push(new DirectorySourceSearchOptions(subDir.FullName, currentSource.WithSubDirectories));
                    }
                }
                catch (OperationCanceledException)
                {

                }
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