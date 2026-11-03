using System.Collections.Generic;
using System.Threading;
using LibGit2Sharp;
using Musoq.DataSources.Common;
using Musoq.DataSources.Git.Entities;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Git;

/// <summary>
/// Common bounded producer, cancellation, and progress behavior for direct Git tables. Sources retain ownership of
/// their traversal semantics while the runtime owns backpressure and early-consumer cancellation.
/// </summary>
internal abstract class GitDiagnosticRowsSourceBase<T> : DiagnosticChunkedRowSource<T>
{
    private readonly SourceExecutionContext _context;
    private readonly string _sourceName;
    private readonly string? _repositoryPath;
    private GitRepositoryContext? _readContext;
    private long _writtenRows;
    internal GitRepositoryLocation? RepositoryLocation { get; set; }

    protected GitDiagnosticRowsSourceBase(SourceExecutionContext context, string sourceName, string? repositoryPath = null)
        : base(context, sourceName)
    {
        _context = context;
        _sourceName = sourceName;
        _repositoryPath = repositoryPath;
    }

    protected sealed override void CollectChunks(DiagnosticChunkWriter<T> writer)
    {
        var token = writer.CancellationToken;
        token.ThrowIfCancellationRequested();
        _context.ReportDataSourceBegin(_sourceName);
        long rowsRead = 0;

        try
        {
            _writtenRows = 0;
            if (_context.Plan.AcceptedTake == 0) return;
            if (_repositoryPath is not null)
                _readContext = new GitRepositoryContext(RepositoryLocation ?? GitRepositoryLocation.Resolve(_repositoryPath),
                    _context.SourceRuntimeSettings, _context.EndWorkToken, _context.Diagnostics);
            if (_readContext?.RequiresCli == true)
            {
                _readContext.EnsureBackend(_sourceName);
                rowsRead = _sourceName is "git.filehistory" or "git.remotetags" or "git.blame"
                    ? CollectRows(writer, token) : CollectCompatibilityRows(writer, _readContext, token);
            }
            else rowsRead = CollectRows(writer, token);
        }
        catch (LibGit2SharpException exception) when (_writtenRows == 0 && _readContext?.TryEnableCli(exception) == true)
        {
            GitDiagnostics.BackendFallback(_context, usingCli: true);
            _readContext.EnsureBackend(_sourceName);
            rowsRead = _sourceName is "git.filehistory" or "git.remotetags" or "git.blame"
                ? CollectRows(writer, token) : CollectCompatibilityRows(writer, _readContext, token);
        }
        finally
        {
            _context.ReportDataSourceEnd(_sourceName, rowsRead);
        }
    }

    protected abstract long CollectRows(DiagnosticChunkWriter<T> writer, CancellationToken cancellationToken);

    protected SourceExecutionContext Context => _context;
    protected GitRepositoryContext? ReadContext => _readContext;

    private long CollectCompatibilityRows(DiagnosticChunkWriter<T> writer, GitRepositoryContext context, CancellationToken token)
    {
        var predicate = TypedPredicatePlan.Get<T>(_context.Plan);
        var projection = GitSourcePlanner.GetProjection(_context.Plan);
        var bindings = GitPatternColumns.Get(_sourceName[4..]) as PatternColumnBindings<T>;
        var rows = new List<T>(128);
        long written = 0, skipped = 0;
        foreach (var row in context.Rows<T>(_sourceName, GitSourcePlanner.GetFilters(_context.Plan).Sha, projection))
        {
            token.ThrowIfCancellationRequested();
            if (predicate?.Invoke(row) == false) continue;
            if (bindings is not null && projection.NativePatterns?.Matches(row, bindings) == false) continue;
            if (skipped < _context.Plan.AcceptedSkip.GetValueOrDefault()) { skipped++; continue; }
            rows.Add(row);
            if (_context.Plan.AcceptedTake is { } take && written + rows.Count >= take) break;
            if (rows.Count == 128) written += WriteChunk(writer, rows, written);
        }
        return written + WriteChunk(writer, rows, written);
    }

    protected int WriteChunk(DiagnosticChunkWriter<T> writer, List<T> rows, long rowsReadBeforeWrite)
    {
        if (rows.Count == 0)
            return 0;

        var count = rows.Count;
        _writtenRows += count;
        foreach (var row in rows)
        {
            switch (row)
            {
                case RepositoryEntity entity: entity.ReadContext = _readContext; break;
                case CommitEntity entity: entity.ReadContext = _readContext; break;
                case BranchEntity entity: entity.ReadContext = _readContext; break;
                case TagEntity entity: entity.ReadContext = _readContext; break;
                case StashEntity entity: entity.ReadContext = _readContext; break;
                case BlameHunkEntity entity: entity.ReadContext = _readContext; break;
            }
        }
        writer.Write(rows.ToArray());
        rows.Clear();
        _context.ReportDataSourceRowsRead(_sourceName, rowsReadBeforeWrite + count);
        return count;
    }
}
