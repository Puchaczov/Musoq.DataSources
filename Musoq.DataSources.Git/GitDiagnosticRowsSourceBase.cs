using System.Collections.Generic;
using System.Threading;
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

    protected GitDiagnosticRowsSourceBase(SourceExecutionContext context, string sourceName)
        : base(context, sourceName)
    {
        _context = context;
        _sourceName = sourceName;
    }

    protected sealed override void CollectChunks(DiagnosticChunkWriter<T> writer)
    {
        var token = writer.CancellationToken;
        token.ThrowIfCancellationRequested();
        _context.ReportDataSourceBegin(_sourceName);
        long rowsRead = 0;

        try
        {
            rowsRead = CollectRows(writer, token);
        }
        finally
        {
            _context.ReportDataSourceEnd(_sourceName, rowsRead);
        }
    }

    protected abstract long CollectRows(DiagnosticChunkWriter<T> writer, CancellationToken cancellationToken);

    protected SourceExecutionContext Context => _context;

    protected int WriteChunk(DiagnosticChunkWriter<T> writer, List<T> rows, long rowsReadBeforeWrite)
    {
        if (rows.Count == 0)
            return 0;

        var count = rows.Count;
        writer.Write(rows.ToArray());
        rows.Clear();
        _context.ReportDataSourceRowsRead(_sourceName, rowsReadBeforeWrite + count);
        return count;
    }
}
