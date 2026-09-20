#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Musoq.DataSources.Common;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

using Musoq.DataSources.Search.Entities;
using Musoq.DataSources.Search.Components.Contracts;
using Musoq.DataSources.Search.Components.Diagnostics;
using Musoq.DataSources.Search.Components.Planning;
using Musoq.DataSources.Search.Components.Text;
using Musoq.DataSources.Search.Components.Traversal;

namespace Musoq.DataSources.Search.Sources;

internal sealed class SearchPathsSource : RowSourceBase<SearchPath>
{
    private const string SearchSourceName = "paths";
    private readonly string _root;
    private readonly ScopePolicy _scope;
    private readonly long _maxFiles;
    private readonly SourceExecutionContext _executionContext;
    private readonly SearchScopeCounters _counters;

    public SearchPathsSource(
        string root,
        SourceExecutionContext executionContext)
        : this(root, executionContext, ScopePolicy.Default, long.MaxValue, null)
    {
    }

    internal SearchPathsSource(
        string root,
        SourceExecutionContext executionContext,
        ScopePolicy scope,
        SearchScopeCounters? counters)
        : this(root, executionContext, scope, long.MaxValue, counters)
    {
    }

    internal SearchPathsSource(
        string root,
        SourceExecutionContext executionContext,
        ScopePolicy scope,
        long maxFiles,
        SearchScopeCounters? counters)
    {
        _root = RequireRoot(root);
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        if (maxFiles < 0)
            throw new ArgumentOutOfRangeException(nameof(maxFiles));

        _maxFiles = maxFiles;
        _executionContext = executionContext ?? throw new ArgumentNullException(nameof(executionContext));
        _counters = counters ?? new SearchScopeCounters();
    }

    internal SearchScopeExplanation? Explanation { get; private set; }

    protected override void CollectChunks(IChunkWriter<SearchPath> writer)
    {
        var progress = new DataSourceProgressReporter(_executionContext, SearchSourceName);
        progress.Begin();
        List<SearchPath> rows = [];
        var totalRowsProcessed = 0L;
        CancellationTokenSource? linkedCancellation = null;
        var cancellationToken = writer.CancellationToken;
        string? resolvedRoot = null;

        try
        {
            Explanation = null;

            if (_executionContext.EndWorkToken.CanBeCanceled &&
                !_executionContext.EndWorkToken.Equals(writer.CancellationToken))
            {
                linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    writer.CancellationToken,
                    _executionContext.EndWorkToken);
                cancellationToken = linkedCancellation.Token;
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                resolvedRoot = Path.GetFullPath(_root);
            }
            catch (ArgumentException exception)
            {
                throw new SearchRequestException(
                    SearchDiagnosticCatalog.InvalidArgument("root"),
                    exception);
            }

            var relativeRoot = File.Exists(resolvedRoot)
                ? Path.GetDirectoryName(resolvedRoot) ?? resolvedRoot
                : resolvedRoot;
            var rootKind = GetRootKind(resolvedRoot);
            var acceptedWindow = SearchSliceWindow.Create(
                _executionContext.Plan.AcceptedSkip,
                _executionContext.Plan.AcceptedTake);

            foreach (var file in SearchScopeTraversal.Enumerate(
                         resolvedRoot,
                         _scope,
                         cancellationToken,
                         _counters))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (checked(totalRowsProcessed + rows.Count) >= _maxFiles)
                    break;

                var relativePath = Path.GetRelativePath(relativeRoot, file)
                    .Replace(Path.DirectorySeparatorChar, '/');
                if (Path.AltDirectorySeparatorChar != Path.DirectorySeparatorChar)
                    relativePath = relativePath.Replace(Path.AltDirectorySeparatorChar, '/');

                var row = new SearchPath(relativePath, null, "file");
                if (_executionContext.Plan.AcceptedPredicate is not null &&
                    !SearchPredicateEvaluator.Matches(
                        _executionContext.Plan.AcceptedPredicate,
                        row,
                        SearchPredicateValues.GetPathValue))
                    continue;

                if (acceptedWindow is not null && !acceptedWindow.TryAccept())
                    continue;

                rows.Add(row);
                progress.RowsRead(1);
                if (rows.Count < RowChunking.DefaultChunkSize)
                    continue;

                WriteRows(writer, rows);
                totalRowsProcessed += rows.Count;
                rows = [];
            }

            if (rows.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                WriteRows(writer, rows);
                totalRowsProcessed += rows.Count;
            }

            Explanation = new SearchScopeExplanation(
                _root,
                resolvedRoot,
                rootKind,
                _scope,
                _counters,
                totalRowsProcessed);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ISearchDiagnosticException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException)
        {
            throw new SearchSourceAccessException(
                SearchDiagnosticCatalog.SourceOpenFailed(resolvedRoot ?? _root),
                exception);
        }
        finally
        {
            linkedCancellation?.Dispose();
            progress.End(totalRowsProcessed);
        }
    }

    private static string RequireRoot(string? root)
    {
        if (string.IsNullOrEmpty(root))
        {
            throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidArgument("root"));
        }

        return root;
    }

    private static string GetRootKind(string rootPath)
    {
        if (File.Exists(rootPath))
            return "file";

        return Directory.Exists(rootPath)
            ? "directory"
            : "missing";
    }

    private static void WriteRows(
        IChunkWriter<SearchPath> writer,
        IReadOnlyList<SearchPath> rows)
    {
        try
        {
            writer.Write(rows);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new SearchOutputException(
                SearchDiagnosticCatalog.OutputFailed(),
                exception);
        }
    }
}
