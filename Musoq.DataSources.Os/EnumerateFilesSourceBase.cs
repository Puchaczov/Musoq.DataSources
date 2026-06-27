using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Os;

internal abstract class EnumerateFilesSourceBase<TEntity>(
    string path,
    bool useSubDirectories,
    SourceExecutionContext executionContext)
    : AsyncRowsSourceBase<TEntity>(executionContext.EndWorkToken)
{
    private const int ChunkSize = 100;
    private readonly SourcePredicateExpression? _acceptedPredicate = executionContext.Plan.AcceptedPredicate;
    private readonly OsFileFilterParameters _fileFilters = OsSourcePlanner.GetFileFilters(executionContext.Plan);

    private readonly DirectorySourceSearchOptions[] _source =
    [
        new(new DirectoryInfo(path).FullName, useSubDirectories)
    ];

    protected virtual string DataSourceName => "files";

    protected override Task CollectChunksAsync(IChunkWriter<TEntity> writer, CancellationToken cancellationToken)
    {
        executionContext.ReportDataSourceBegin(DataSourceName);
        long totalRowsProcessed = 0;

        try
        {
            foreach (var source in _source)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!Directory.Exists(source.Path))
                    continue;

                foreach (var chunk in EnumerateChunks(source, cancellationToken))
                {
                    writer.Write(chunk);
                    totalRowsProcessed += chunk.Count;
                }
            }
        }
        finally
        {
            executionContext.ReportDataSourceEnd(DataSourceName, totalRowsProcessed);
        }

        return Task.CompletedTask;
    }

    private IEnumerable<IReadOnlyList<TEntity>> EnumerateChunks(
        DirectorySourceSearchOptions source,
        CancellationToken cancellationToken)
    {
        var sources = new Stack<DirectorySourceSearchOptions>();
        var chunk = new List<TEntity>(ChunkSize);
        sources.Push(source);

        while (sources.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var currentSource = sources.Pop();
            var dir = new DirectoryInfo(currentSource.Path);

            FileInfo[] files;
            try
            {
                files = GetFiles(dir);
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (DirectoryNotFoundException)
            {
                continue;
            }

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ProcessFile(file, source, chunk);

                if (chunk.Count < ChunkSize)
                    continue;

                yield return chunk;
                chunk = [];
            }

            if (!currentSource.WithSubDirectories)
                continue;

            DirectoryInfo[] subDirectories;
            try
            {
                subDirectories = dir.GetDirectories();
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (DirectoryNotFoundException)
            {
                continue;
            }

            foreach (var subDir in subDirectories)
                sources.Push(new DirectorySourceSearchOptions(subDir.FullName, currentSource.WithSubDirectories));
        }

        if (chunk.Count > 0)
            yield return chunk;
    }

    protected virtual FileInfo[] GetFiles(DirectoryInfo directoryInfo)
    {
        var searchPattern = _fileFilters.GetSearchPattern();
        if (searchPattern is not null)
            return directoryInfo.GetFiles(searchPattern);

        return directoryInfo.GetFiles();
    }

    protected virtual void ProcessFile(FileInfo file, DirectorySourceSearchOptions source, List<TEntity> dirFiles)
    {
        var entity = CreateBasedOnFile(file, source.Path);

        if (entity != null && OsSourcePlanner.Matches(_acceptedPredicate, entity))
            dirFiles.Add(entity);
    }

    protected virtual TEntity? CreateBasedOnFile(FileInfo file, string rootDirectory)
    {
        return default;
    }
}
