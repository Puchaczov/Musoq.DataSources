using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Threading;
using System.Threading.Tasks;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.DataSources.Common;
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

    protected NativePatternExecution? NativePatterns { get; } = NativePatternExecution.Get(executionContext);
    protected virtual PatternColumnBindings<string> NativeBindings => OsNativePatterns.Files;
    protected bool AdmitPath(string fullPath) => NativePatterns?.Matches(fullPath, NativeBindings) != false;

    protected virtual string DataSourceName => "files";

    protected override Task CollectChunksAsync(IChunkWriter<TEntity> writer, CancellationToken cancellationToken)
    {
        var progress = new DataSourceProgressReporter(executionContext, DataSourceName);
        progress.Begin();
        long totalRowsProcessed = 0;

        try
        {
            foreach (var source in _source)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!Directory.Exists(source.Path))
                    continue;

                foreach (var chunk in EnumerateChunks(source, progress, cancellationToken))
                {
                    writer.Write(chunk);
                    totalRowsProcessed += chunk.Count;
                }
            }
        }
        finally
        {
            progress.End(totalRowsProcessed);
        }

        return Task.CompletedTask;
    }

    private IEnumerable<IReadOnlyList<TEntity>> EnumerateChunks(
        DirectorySourceSearchOptions source,
        DataSourceProgressReporter progress,
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

            IEnumerator<FileInfo> files;
            try
            {
                files = GetFiles(dir, cancellationToken).GetEnumerator();
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (DirectoryNotFoundException)
            {
                continue;
            }
            catch (PathTooLongException)
            {
                continue;
            }

            var fileEnumerationFailed = false;
            using (files)
            {
                while (true)
                {
                    bool hasNext;
                    try
                    {
                        hasNext = files.MoveNext();
                    }
                    catch (UnauthorizedAccessException)
                    {
                        fileEnumerationFailed = true;
                        break;
                    }
                    catch (DirectoryNotFoundException)
                    {
                        fileEnumerationFailed = true;
                        break;
                    }
                    catch (PathTooLongException)
                    {
                        fileEnumerationFailed = true;
                        break;
                    }

                    if (!hasNext)
                        break;

                    var file = files.Current;
                    cancellationToken.ThrowIfCancellationRequested();
                    progress.RowRead();

                    if (!OsSourcePlanner.MatchesFilePredicate(_acceptedPredicate, file))
                        continue;

                    ProcessFile(file, source, chunk);

                    if (chunk.Count < ChunkSize)
                        continue;

                    yield return chunk;
                    chunk = [];
                }
            }

            if (fileEnumerationFailed)
                continue;

            if (!currentSource.WithSubDirectories)
                continue;

            try
            {
                foreach (var subDir in dir.EnumerateDirectories())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    sources.Push(new DirectorySourceSearchOptions(subDir.FullName, currentSource.WithSubDirectories));
                }
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (DirectoryNotFoundException)
            {
                continue;
            }
            catch (PathTooLongException)
            {
                continue;
            }
        }

        if (chunk.Count > 0)
            yield return chunk;
    }

    protected virtual IEnumerable<FileInfo> GetFiles(DirectoryInfo directoryInfo, CancellationToken cancellationToken)
    {
        var searchPattern = _fileFilters.GetSearchPattern();
        if (NativePatterns?.HasPredicates == true)
        {
            var expression = searchPattern is null ? null : FileSystemName.TranslateWin32Expression(searchPattern);
            return new FileSystemEnumerable<FileInfo>(directoryInfo.FullName,
                static (ref FileSystemEntry entry) => (FileInfo)entry.ToFileSystemInfo(),
                new EnumerationOptions { AttributesToSkip = 0, IgnoreInaccessible = false })
            {
                ShouldIncludePredicate = (ref FileSystemEntry entry) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return !entry.IsDirectory &&
                        (expression is null || FileSystemName.MatchesWin32Expression(expression, entry.FileName, OperatingSystem.IsWindows())) &&
                        AdmitPath(entry.ToFullPath());
                }
            };
        }
        return GetFiles(directoryInfo);
    }

    protected virtual IEnumerable<FileInfo> GetFiles(DirectoryInfo directoryInfo)
    {
        var searchPattern = _fileFilters.GetSearchPattern();
        if (searchPattern is not null)
            return directoryInfo.EnumerateFiles(searchPattern);

        return directoryInfo.EnumerateFiles();
    }

    protected virtual void ProcessFile(FileInfo file, DirectorySourceSearchOptions source, List<TEntity> dirFiles)
    {
        var entity = CreateBasedOnFile(file, source.Path);

        if (entity != null)
        {
            NativePatterns?.Constructed();
            dirFiles.Add(entity);
        }
    }

    protected virtual TEntity? CreateBasedOnFile(FileInfo file, string rootDirectory)
    {
        return default;
    }
}
