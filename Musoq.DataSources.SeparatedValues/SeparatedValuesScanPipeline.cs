#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Musoq.DataSources.Common;
using Musoq.DataSources.Structured;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues;

internal sealed class SeparatedValuesScanPipeline : ISeparatedValuesScanPipeline
{
    private const int EarlyTakeInputBufferSize = 64 * 1024;
    private const int EarlyTakeRowLimit = 4096;
    private const int SequentialInputBufferSize = 1024 * 1024;
    private const int ZeroColumnChunkRows = 1024 * 1024;
    private const string SourceName = "separated_values";
    private readonly bool _forceParallel;
    private readonly SeparatedValuesParallelBlockScanPipeline _parallelPipeline;

    public SeparatedValuesScanPipeline(
        SeparatedValuesParallelBlockScanPipeline? parallelPipeline = null,
        bool forceParallel = false)
    {
        _parallelPipeline = parallelPipeline ?? new SeparatedValuesParallelBlockScanPipeline();
        _forceParallel = forceParallel;
    }

    public void Execute(SeparatedValuesScanRequest request, IChunkWriter<object?[]> writer)
    {
        var progress = new DataSourceProgressReporter(request.ExecutionContext, SourceName);
        progress.Begin();
        CancellationTokenSource? linkedCancellation = null;
        long rowsEmitted = 0;

        try
        {
            if (request.ExecutionContext.EndWorkToken.IsCancellationRequested ||
                writer.CancellationToken.IsCancellationRequested)
                return;

            var cancellationToken = writer.CancellationToken;
            if (request.ExecutionContext.EndWorkToken.CanBeCanceled &&
                !request.ExecutionContext.EndWorkToken.Equals(writer.CancellationToken))
            {
                linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    writer.CancellationToken,
                    request.ExecutionContext.EndWorkToken);
                cancellationToken = linkedCancellation.Token;
            }

            var contract = ResolveContract(request, cancellationToken);
            var snapshot = contract.Snapshot;
            EnsurePlanStillMatches(snapshot, request.ExecutionContext);
            if (contract.HasExactCardinality)
                progress.RowsKnown(snapshot.RowCount);

            var readPlan = SeparatedValuesReadPlan.From(request.ExecutionContext.Plan);
            var projectedColumns = readPlan.ProjectionAccepted
                ? request.ExecutionContext.Plan.AcceptedColumns.Count
                : request.ExecutionContext.AllColumns.Count > 0
                    ? request.ExecutionContext.AllColumns.Count
                    : snapshot.Columns.Length;
            var strategy = SeparatedValuesReadStrategySelector.Select(new SeparatedValuesReadStrategyContext(
                snapshot.Identity.Length,
                projectedColumns,
                snapshot.Columns.Length,
                request.ExecutionContext.Plan.AcceptedTake,
                readPlan.HasResidualWork,
                readPlan.ProjectionAccepted));
            progress.SetRowsReadReportInterval(strategy.RowChunkSize);

            if (request.ExecutionContext.Plan.AcceptedTake is 0)
                return;

            var maximumParallelism = SeparatedValuesParallelScanOptions.Resolve(contract, request.ExecutionContext);
            if (_forceParallel)
                maximumParallelism = Math.Max(2, maximumParallelism);
            if (!(request.Dialect ?? contract.Dialect).IsParallelFramingCompatible)
                maximumParallelism = 1;
            if (maximumParallelism > 1)
            {
                rowsEmitted = _parallelPipeline.Execute(
                    request,
                    contract,
                    writer,
                    progress,
                    strategy.RowChunkSize,
                    maximumParallelism,
                    cancellationToken);
                return;
            }

            if (contract.HasExactCardinality && CanUseZeroColumnScan(readPlan, request.ExecutionContext))
            {
                progress.RowsRead(snapshot.RowCount);
                WriteRepeatedRows(writer, snapshot.RowCount, ZeroColumnChunkRows);
                rowsEmitted = snapshot.RowCount;
                return;
            }

            rowsEmitted = ProcessSequential(
                request,
                contract,
                writer,
                progress,
                strategy.RowChunkSize,
                cancellationToken);
        }
        finally
        {
            linkedCancellation?.Dispose();
            progress.End(rowsEmitted);
        }
    }

    private static SeparatedValuesSourceContract ResolveContract(
        SeparatedValuesScanRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var planned = SeparatedValuesSourceContract.From(request.ExecutionContext.Plan);
            var currentIdentity = StructuredFileIdentity.Capture(
                request.Path,
                planned.Snapshot.Identity.ParserOptions,
                cancellationToken);
            if (request.Dialect is not null)
            {
                var expectedParserOptions = SeparatedValuesFormat.CreateParserOptions(
                    request.Dialect,
                    request.HasHeader,
                    request.SkipLines);
                if (!string.Equals(
                        planned.Snapshot.Identity.ParserOptions,
                        expectedParserOptions,
                        StringComparison.Ordinal))
                {
                    throw new StructuredSchemaDriftException(
                        planned.Snapshot.Identity.CanonicalPath,
                        "the dialect changed after planning");
                }
            }
            if (!StructuredFileIdentityComparer.Instance.Equals(planned.Snapshot.Identity, currentIdentity))
            {
                throw new StructuredSchemaDriftException(
                    planned.Snapshot.Identity.CanonicalPath,
                    "the file identity changed after planning");
            }

            return planned;
        }
        catch (InvalidOperationException exception) when
            (exception is not StructuredSchemaDriftException)
        {
            return SeparatedValuesPipelineModules.Default.SchemaResolver.Resolve(
                new SeparatedValuesSchemaResolutionRequest(
                    request.Path,
                    request.Separator,
                    request.HasHeader,
                    request.SkipLines,
                    request.ExecutionContext.AllColumns,
                    request.ExecutionContext.SourceRuntimeSettings,
                    cancellationToken,
                    request.Dialect));
        }
    }

    private static bool CanUseZeroColumnScan(
        SeparatedValuesReadPlan readPlan,
        SourceExecutionContext executionContext)
    {
        var plan = executionContext.Plan;
        return readPlan.ProjectionAccepted &&
               !readPlan.HasResidualWork &&
               plan.AcceptedColumns.Count == 0 &&
               plan.AcceptedPredicate is null &&
               plan.AcceptedSkip is null &&
               plan.AcceptedTake is null;
    }

    private static void WriteRepeatedRows(
        IChunkWriter<object?[]> writer,
        long rowCount,
        int chunkSize)
    {
        while (rowCount > 0)
        {
            var count = (int)Math.Min(chunkSize, rowCount);
            writer.Write(new RepeatedValueChunk<object?[]>(Array.Empty<object?>(), count));
            rowCount -= count;
        }
    }

    private static long ProcessSequential(
        SeparatedValuesScanRequest request,
        SeparatedValuesSourceContract contract,
        IChunkWriter<object?[]> writer,
        DataSourceProgressReporter progress,
        int chunkSize,
        CancellationToken cancellationToken)
    {
        var snapshot = contract.Snapshot;
        var dialect = request.Dialect ?? contract.Dialect;
        var rowNumberOffset = 0L;
        long? skipOverride = null;
        var consumeHeader = request.HasHeader;
        SeparatedValuesUtf8Reader reader;
        SeparatedValuesStructuralSummaryBuilder? summaryBuilder = null;

        var acceptedSkip = request.ExecutionContext.Plan.AcceptedSkip ?? 0;
        if (acceptedSkip > 0 &&
            request.ExecutionContext.Plan.AcceptedPredicate is null &&
            contract.StructuralSummary is not null)
        {
            if (!contract.StructuralSummary.TryFindRow(acceptedSkip, out var block))
                return 0;

            rowNumberOffset = block.StartRow;
            skipOverride = acceptedSkip - block.StartRow;
            consumeHeader = false;
            reader = new SeparatedValuesUtf8Reader(
                snapshot.Identity.CanonicalPath,
                dialect,
                block.FirstRecordOffset,
                snapshot.Identity.Length,
                cancellationToken);
        }
        else
        {
            reader = new SeparatedValuesUtf8Reader(
                snapshot.Identity.CanonicalPath,
                dialect,
                request.SkipLines,
                request.ExecutionContext.Plan.AcceptedTake is > 0 and <= EarlyTakeRowLimit &&
                !request.ExecutionContext.Plan.AcceptedSkip.HasValue
                    ? EarlyTakeInputBufferSize
                    : SequentialInputBufferSize,
                cancellationToken);
            if (contract.StructuralSummary is null)
            {
                summaryBuilder = new SeparatedValuesStructuralSummaryBuilder(
                    snapshot.Identity,
                    contract.DataStartOffset);
            }
        }

        using (reader)
        {
            if (consumeHeader && !reader.TryRead(out _))
                throw new StructuredSchemaDriftException(snapshot.Identity.CanonicalPath, "the header disappeared");

            var processor = new SeparatedValuesRowProcessor(
                contract,
                request.ExecutionContext,
                writer,
                progress,
                chunkSize,
                cancellationToken,
                rowNumberOffset,
                skipOverride);
            var completedInput = true;
            while (reader.TryRead(out var record))
            {
                summaryBuilder?.ObserveRecord(record.StartOffset, record.EndOffset);
                if (processor.Process(record))
                    continue;
                completedInput = false;
                break;
            }

            processor.Complete();
            if (completedInput && summaryBuilder is not null)
                SeparatedValuesStructuralSummaryCache.Store(summaryBuilder.Build());
            return processor.RowsEmitted;
        }
    }

    private static void EnsurePlanStillMatches(
        StructuredSchemaSnapshot snapshot,
        SourceExecutionContext executionContext)
    {
        if (executionContext.Plan.Properties is null ||
            !executionContext.Plan.Properties.TryGetValue(SeparatedValuesPlanning.LayoutPropertyName, out var value) ||
            value is not StructuredExecutionLayout layout)
            return;

        layout.EnsureCompatibleWith(snapshot);
    }
}
