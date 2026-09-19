#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

using Musoq.DataSources.Search.Components.Diagnostics;

namespace Musoq.DataSources.Search.Components.Traversal;

internal readonly record struct SearchTraversalEntry(
    string Path,
    bool IsDirectory,
    bool IsFile)
{
    public bool IsReparsePoint { get; init; }

    public bool IsSpecial { get; init; }

    public bool IsHidden { get; init; }
}

internal enum SearchLinkResolutionStatus
{
    Resolved,
    Broken,
    Cycle,
    Special
}

internal readonly record struct SearchLinkResolution(
    SearchLinkResolutionStatus Status,
    string? PhysicalPath,
    bool IsDirectory,
    bool IsFile);

internal enum SearchTraversalAction
{
    Skip,
    Descend,
    Yield
}

internal static class SearchFileTraversal
{
    internal static IEnumerable<string> Enumerate(
        string rootPath,
        CancellationToken cancellationToken,
        Func<SearchTraversalEntry, SearchTraversalAction>? entryPolicy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        cancellationToken.ThrowIfCancellationRequested();

        if (File.Exists(rootPath))
        {
            // An explicit file root is the complete candidate set and bypasses
            // directory-scoped ignore and pruning rules.
            cancellationToken.ThrowIfCancellationRequested();
            yield return rootPath;
            yield break;
        }

        if (!Directory.Exists(rootPath))
        {
            throw new SearchSourceAccessException(
                SearchDiagnosticCatalog.MissingRoot(rootPath),
                new DirectoryNotFoundException());
        }

        foreach (var file in EnumerateDirectory(
                     rootPath,
                     cancellationToken,
                     OpenDirectoryEntries,
                     entryPolicy))
        {
            yield return file;
        }
    }

    internal static IEnumerable<string> EnumerateDirectory(
        string directoryPath,
        CancellationToken cancellationToken,
        Func<string, IEnumerator<SearchTraversalEntry>> directoryEnumeratorFactory,
        Func<SearchTraversalEntry, SearchTraversalAction>? entryPolicy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentNullException.ThrowIfNull(directoryEnumeratorFactory);

        var frontier = new Stack<TraversalFrame>();
        try
        {
            frontier.Push(OpenFrame(directoryPath, directoryEnumeratorFactory));

            while (frontier.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var frame = frontier.Peek();
                bool hasEntry;
                try
                {
                    hasEntry = frame.Entries.MoveNext();
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception) when (exception is ISearchDiagnosticException)
                {
                    throw;
                }
                catch (Exception exception) when (IsTraversalAccessFailure(exception))
                {
                    throw new SearchSourceAccessException(
                        SearchDiagnosticCatalog.SourceOpenFailed(frame.Path),
                        exception);
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (!hasEntry)
                {
                    frontier.Pop().Dispose();
                    continue;
                }

                var entry = frame.Entries.Current;
                var action = entryPolicy?.Invoke(entry) ?? DefaultAction(entry);
                switch (action)
                {
                    case SearchTraversalAction.Skip:
                        continue;

                    case SearchTraversalAction.Descend:
                        if (!entry.IsDirectory)
                            continue;

                        cancellationToken.ThrowIfCancellationRequested();
                        frontier.Push(OpenFrame(entry.Path, directoryEnumeratorFactory));
                        continue;

                    case SearchTraversalAction.Yield:
                        if (entry.IsFile)
                            yield return entry.Path;

                        continue;

                    default:
                        throw new InvalidOperationException(
                            $"Unknown traversal action '{action}'.");
                }
            }
        }
        finally
        {
            while (frontier.Count > 0)
                frontier.Pop().Dispose();
        }
    }

    private static SearchTraversalAction DefaultAction(SearchTraversalEntry entry)
    {
        if (entry.IsReparsePoint || entry.IsSpecial)
            return SearchTraversalAction.Skip;

        if (entry.IsDirectory)
            return SearchTraversalAction.Descend;

        return entry.IsFile
            ? SearchTraversalAction.Yield
            : SearchTraversalAction.Skip;
    }

    private static TraversalFrame OpenFrame(
        string directoryPath,
        Func<string, IEnumerator<SearchTraversalEntry>> directoryEnumeratorFactory)
    {
        try
        {
            var entries = directoryEnumeratorFactory(directoryPath);
            ArgumentNullException.ThrowIfNull(entries);
            return new TraversalFrame(directoryPath, entries);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ISearchDiagnosticException)
        {
            throw;
        }
        catch (Exception exception) when (IsTraversalAccessFailure(exception))
        {
            throw new SearchSourceAccessException(
                SearchDiagnosticCatalog.SourceOpenFailed(directoryPath),
                exception);
        }
    }

    private static IEnumerator<SearchTraversalEntry> OpenDirectoryEntries(string directoryPath)
    {
        return Directory
            .EnumerateFileSystemEntries(directoryPath)
            .Select(static path => CreateEntry(path))
            .GetEnumerator();
    }

    private static SearchTraversalEntry CreateEntry(string path)
    {
        return ClassifyPath(path);
    }

    internal static SearchTraversalEntry ClassifyPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        // LinkTarget is queried separately because some Unix runtimes expose
        // a symlink's target while GetAttributes follows the link. On
        // Windows, FileAttributes.ReparsePoint remains the authoritative
        // signal for junctions and other reparse points.
        string? linkTarget = null;
        try
        {
            linkTarget = new FileInfo(path).LinkTarget;
        }
        catch (Exception exception) when (IsTraversalAccessFailure(exception))
        {
            // GetAttributes below can still classify the entry. If it also
            // fails, its exception is the more useful source diagnostic.
        }

        try
        {
            var attributes = File.GetAttributes(path);
            var isDirectory = attributes.HasFlag(FileAttributes.Directory);
            var isReparsePoint =
                attributes.HasFlag(FileAttributes.ReparsePoint) ||
                linkTarget is not null;
            if (isReparsePoint && !isDirectory)
                isDirectory = Directory.Exists(path);

            var isSpecial =
                !isReparsePoint &&
                attributes.HasFlag(FileAttributes.Device);
            var fileName = System.IO.Path.GetFileName(path);
            var isHidden =
                attributes.HasFlag(FileAttributes.Hidden) ||
                (fileName.Length > 0 && fileName[0] == '.');

            return new SearchTraversalEntry(
                path,
                IsDirectory: isDirectory,
                IsFile: !isDirectory && !isSpecial)
            {
                IsReparsePoint = isReparsePoint,
                IsSpecial = isSpecial,
                IsHidden = isHidden
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (linkTarget is not null && IsTraversalAccessFailure(exception))
        {
            // A broken symlink may not have target attributes. It is still a
            // reparse entry and the scope policy will decide whether to skip
            // it or attempt resolution.
            return new SearchTraversalEntry(path, IsDirectory: false, IsFile: true)
            {
                IsReparsePoint = true,
                IsHidden = Path.GetFileName(path) is { Length: > 0 } name && name[0] == '.'
            };
        }
        catch (Exception exception) when (IsTraversalAccessFailure(exception))
        {
            throw new SearchSourceAccessException(
                SearchDiagnosticCatalog.SourceOpenFailed(path),
                exception);
        }
    }

    internal static SearchLinkResolution ResolveLink(
        SearchTraversalEntry entry,
        CancellationToken cancellationToken = default)
    {
        if (!entry.IsReparsePoint)
        {
            return new SearchLinkResolution(
                SearchLinkResolutionStatus.Resolved,
                Path.GetFullPath(entry.Path),
                entry.IsDirectory,
                entry.IsFile);
        }

        var currentPath = Path.GetFullPath(entry.Path);
        var currentIsDirectory = entry.IsDirectory;
        var visited = new HashSet<string>(GetPathComparer());

        for (var depth = 0; depth < 128; depth++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!visited.Add(currentPath))
            {
                return new SearchLinkResolution(
                    SearchLinkResolutionStatus.Cycle,
                    null,
                    false,
                    false);
            }

            FileSystemInfo fileSystemInfo = currentIsDirectory
                ? new DirectoryInfo(currentPath)
                : new FileInfo(currentPath);

            FileSystemInfo? target;
            try
            {
                target = fileSystemInfo.ResolveLinkTarget(returnFinalTarget: false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (FileNotFoundException)
            {
                return BrokenLink();
            }
            catch (DirectoryNotFoundException)
            {
                return BrokenLink();
            }
            catch (Exception exception) when (IsTraversalAccessFailure(exception))
            {
                throw new SearchSourceAccessException(
                    SearchDiagnosticCatalog.SourceOpenFailed(entry.Path),
                    exception);
            }

            if (target is null)
            {
                // A reparse point without a resolvable target is not a
                // regular input. Treat it as an excluded broken/unsupported
                // link rather than opening it as content.
                return BrokenLink();
            }

            var targetPath = Path.GetFullPath(target.FullName);
            SearchTraversalEntry targetEntry;
            try
            {
                targetEntry = ClassifyPath(targetPath);
            }
            catch (SearchSourceAccessException exception)
                when (exception.InnerException is FileNotFoundException or DirectoryNotFoundException)
            {
                return BrokenLink();
            }

            if (targetEntry.IsSpecial)
            {
                return new SearchLinkResolution(
                    SearchLinkResolutionStatus.Special,
                    null,
                    false,
                    false);
            }

            if (!targetEntry.IsReparsePoint)
            {
                return new SearchLinkResolution(
                    SearchLinkResolutionStatus.Resolved,
                    targetPath,
                    targetEntry.IsDirectory,
                    targetEntry.IsFile);
            }

            currentPath = targetPath;
            currentIsDirectory = targetEntry.IsDirectory;
        }

        return new SearchLinkResolution(
            SearchLinkResolutionStatus.Cycle,
            null,
            false,
            false);

        static SearchLinkResolution BrokenLink()
        {
            return new SearchLinkResolution(
                SearchLinkResolutionStatus.Broken,
                null,
                false,
                false);
        }
    }

    private static StringComparer GetPathComparer()
    {
        return OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
    }

    private static bool IsTraversalAccessFailure(Exception exception)
    {
        return exception is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException;
    }

    private sealed class TraversalFrame(
        string path,
        IEnumerator<SearchTraversalEntry> entries) : IDisposable
    {
        public string Path { get; } = path;

        public IEnumerator<SearchTraversalEntry> Entries { get; } = entries;

        public void Dispose()
        {
            Entries.Dispose();
        }
    }
}
