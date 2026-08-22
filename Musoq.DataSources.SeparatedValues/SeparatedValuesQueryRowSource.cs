#nullable enable

using System;
using System.IO;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues;

internal sealed class SeparatedValuesQueryRowSource<TRow, TMaterializer> : RowSourceBase<TRow>
    where TMaterializer : struct, IQueryRowMaterializer<TRow>
{
    private readonly SeparatedValuesScanRequest _request;
    private readonly QueryRowShape _shape;
    private readonly ISeparatedValuesQueryScanPipeline _scanPipeline;

    public SeparatedValuesQueryRowSource(
        string filePath,
        string separator,
        bool hasHeader,
        int skipLines,
        QueryScopedRowSourceRequest request,
        ISeparatedValuesQueryScanPipeline scanPipeline,
        SeparatedValuesDialect? dialect = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(separator);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(scanPipeline);
        ArgumentOutOfRangeException.ThrowIfNegative(skipLines);
        if (separator.Length != 1 || separator[0] > 0x7f)
            throw new ArgumentException("The separator must be one ASCII character.", nameof(separator));

        _request = new SeparatedValuesScanRequest(
            Path.GetFullPath(filePath),
            separator,
            checked((byte)separator[0]),
            hasHeader,
            skipLines,
            request.ExecutionContext,
            dialect);
        _shape = request.Shape;
        _scanPipeline = scanPipeline;
    }

    protected override void CollectChunks(IChunkWriter<TRow> writer)
    {
        _scanPipeline.Execute<TRow, TMaterializer>(_request, _shape, writer);
    }
}
