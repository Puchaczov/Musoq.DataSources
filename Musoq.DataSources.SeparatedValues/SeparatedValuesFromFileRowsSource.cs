#nullable enable

using System;
using System.IO;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues;

internal sealed class SeparatedValuesFromFileRowsSource : RowSourceBase<object?[]>
{
    private readonly SeparatedValuesScanRequest _request;
    private readonly ISeparatedValuesScanPipeline _scanPipeline;

    public SeparatedValuesFromFileRowsSource(
        string filePath,
        string separator,
        bool hasHeader,
        int skipLines,
        SourceExecutionContext executionContext)
        : this(
            filePath,
            separator,
            hasHeader,
            skipLines,
            executionContext,
            SeparatedValuesPipelineModules.Default.ScanPipeline,
            null)
    {
    }

    internal SeparatedValuesFromFileRowsSource(
        string filePath,
        string separator,
        bool hasHeader,
        int skipLines,
        SourceExecutionContext executionContext,
        ISeparatedValuesScanPipeline scanPipeline,
        SeparatedValuesDialect? dialect = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(separator);
        ArgumentNullException.ThrowIfNull(executionContext);
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
            executionContext,
            dialect);
        _scanPipeline = scanPipeline;
    }

    protected override void CollectChunks(IChunkWriter<object?[]> writer)
    {
        _scanPipeline.Execute(_request, writer);
    }
}
