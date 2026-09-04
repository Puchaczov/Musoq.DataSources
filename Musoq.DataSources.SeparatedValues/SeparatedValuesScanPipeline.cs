#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading;
using Musoq.DataSources.Common;
using Musoq.DataSources.Structured;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues;

internal sealed class SeparatedValuesScanPipeline : ISeparatedValuesQueryScanPipeline
{
    private const int EarlyTakeInputBufferSize = 64 * 1024;
    private const int EarlyTakeRowLimit = 4096;
    private const int MinimumSequentialInputBufferSize = 4 * 1024;
    private const int SmallSequentialQueryChunkRows = 512;
    private const int SequentialInputBufferSize = 1024 * 1024;
    private const int ZeroFieldChunkRows = 16 * 1024;
    private const string SourceName = "separated_values";
    private readonly bool _forceParallel;
    private readonly ISeparatedValuesParallelQueryScanPipeline _parallelPipeline;

    public SeparatedValuesScanPipeline(
        ISeparatedValuesParallelQueryScanPipeline? parallelPipeline = null,
        bool forceParallel = false)
    {
        _parallelPipeline = parallelPipeline ?? new SeparatedValuesParallelBlockScanPipeline();
        _forceParallel = forceParallel;
    }

    public void Execute<TRow, TMaterializer>(
        SeparatedValuesScanRequest request,
        QueryRowShape shape,
        IChunkWriter<TRow> writer)
        where TMaterializer : struct, IQueryRowMaterializer<TRow>
    {
        ArgumentNullException.ThrowIfNull(shape);
        var progress = new DataSourceProgressReporter(request.ExecutionContext, SourceName);
        progress.Begin();
        CancellationTokenSource? linkedCancellation = null;
        long rowsEmitted = 0;

        try
        {
            if (request.ExecutionContext.EndWorkToken.IsCancellationRequested ||
                writer.CancellationToken.IsCancellationRequested)
            {
                request.ExecutionContext.Diagnostics.AddMetric(
                    SeparatedValuesDiagnostics.ExecutionCancellations,
                    1);
                return;
            }

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
            if (request.ExecutionContext.Plan.AcceptedTake is 0)
                return;

            if (request.ExecutionContext.Plan.Properties is null ||
                !request.ExecutionContext.Plan.Properties.TryGetValue(
                    SeparatedValuesPlanning.LayoutPropertyName,
                    out var layoutValue) ||
                layoutValue is not StructuredExecutionLayout layout)
            {
                throw QueryShapeMismatch(
                    request.ExecutionContext.Plan.Identity,
                    shape.Fingerprint,
                    "the accepted execution plan has no immutable physical-column layout");
            }

            if (!SeparatedValuesQueryShapeMapping.TryCreate(
                    contract,
                    layout,
                    request.ExecutionContext.AllColumns,
                    shape,
                    out var mapping,
                    out var reason))
            {
                throw QueryShapeMismatch(
                    request.ExecutionContext.Plan.Identity,
                    shape.Fingerprint,
                    reason);
            }

            var readPlan = SeparatedValuesReadPlan.From(request.ExecutionContext.Plan);
            if (contract.HasExactCardinality &&
                shape.Fields.Count == 0 &&
                CanUseExactZeroFieldScan(readPlan, request.ExecutionContext))
            {
                progress.RowsRead(snapshot.RowCount);
                rowsEmitted = WriteExactZeroFieldRows<TRow, TMaterializer>(
                    writer,
                    snapshot.RowCount,
                    request.ExecutionContext.Plan.Identity,
                    shape.Fingerprint,
                    cancellationToken);
                return;
            }

            var strategy = SeparatedValuesReadStrategySelector.Select(new SeparatedValuesReadStrategyContext(
                snapshot.Identity.Length,
                shape.Fields.Count,
                snapshot.Columns.Length,
                request.ExecutionContext.Plan.AcceptedTake,
                readPlan.HasResidualWork,
                readPlan.ProjectionAccepted));
            var sequentialChunkSize = SelectSequentialQueryChunkSize(
                snapshot.Identity.Length,
                strategy.RowChunkSize);
            progress.SetRowsReadReportInterval(sequentialChunkSize);
            var maximumParallelism = SeparatedValuesParallelScanOptions.Resolve(contract, request.ExecutionContext);
            if (_forceParallel &&
                SeparatedValuesParallelScanOptions.IsParallelShapeSupported(request.ExecutionContext))
                maximumParallelism = Math.Max(2, maximumParallelism);
            if (!(request.Dialect ?? contract.Dialect).IsParallelFramingCompatible)
                maximumParallelism = 1;
            if (maximumParallelism > 1)
            {
                EnsureContractFingerprint(request, contract, cancellationToken);
                rowsEmitted = _parallelPipeline.Execute<TRow, TMaterializer>(
                    request,
                    contract,
                    mapping!,
                    shape,
                    writer,
                    progress,
                    strategy.RowChunkSize,
                    maximumParallelism,
                    cancellationToken);
                return;
            }

            rowsEmitted = ProcessSequentialQuery<TRow, TMaterializer>(
                request,
                contract,
                mapping!,
                writer,
                progress,
                sequentialChunkSize,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            request.ExecutionContext.Diagnostics.AddMetric(
                SeparatedValuesDiagnostics.ExecutionCancellations,
                1);
            throw;
        }
        catch
        {
            request.ExecutionContext.Diagnostics.AddMetric(
                SeparatedValuesDiagnostics.ExecutionFailures,
                1);
            throw;
        }
        finally
        {
            linkedCancellation?.Dispose();
            progress.End(rowsEmitted);
        }
    }

    private static bool CanUseExactZeroFieldScan(
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

    private static long WriteExactZeroFieldRows<TRow, TMaterializer>(
        IChunkWriter<TRow> writer,
        long rowCount,
        SourceIdentity identity,
        string shapeFingerprint,
        CancellationToken cancellationToken)
        where TMaterializer : struct, IQueryRowMaterializer<TRow>
    {
        var remaining = rowCount;
        var reader = new EmptyQueryFieldReader(identity, shapeFingerprint);
        while (remaining > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = (int)Math.Min(ZeroFieldChunkRows, remaining);
            var chunk = new List<TRow>(count);
            for (var index = 0; index < count; index++)
                chunk.Add(TMaterializer.Materialize<EmptyQueryFieldReader>(ref reader));
            writer.Write(chunk);
            remaining -= count;
        }

        return rowCount;
    }

    private ref struct EmptyQueryFieldReader : IQuerySourceFieldReader
    {
        private readonly SourceIdentity _identity;
        private readonly string _shapeFingerprint;

        public EmptyQueryFieldReader(SourceIdentity identity, string shapeFingerprint)
        {
            _identity = identity;
            _shapeFingerprint = shapeFingerprint;
        }

        public T Read<T>(int slot)
        {
            throw QueryShapeMismatch(
                _identity,
                _shapeFingerprint,
                $"the zero-field materializer requested unavailable slot {slot}");
        }
    }

    private static SeparatedValuesSourceContract ResolveContract(
        SeparatedValuesScanRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var planned = SeparatedValuesSourceContract.From(request.ExecutionContext.Plan);
            if (!File.Exists(request.Path))
            {
                var canonicalPath = Path.GetFullPath(request.Path);
                throw new FileNotFoundException(
                    $"Structured source '{canonicalPath}' does not exist.",
                    canonicalPath);
            }
            if (!planned.Snapshot.Identity.MatchesCurrentMetadata(request.Path, cancellationToken))
            {
                throw new StructuredSchemaDriftException(
                    planned.Snapshot.Identity.CanonicalPath,
                    "the file identity changed after planning");
            }
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

    private static long ProcessSequentialQuery<TRow, TMaterializer>(
        SeparatedValuesScanRequest request,
        SeparatedValuesSourceContract contract,
        SeparatedValuesQueryShapeMapping mapping,
        IChunkWriter<TRow> writer,
        DataSourceProgressReporter progress,
        int chunkSize,
        CancellationToken cancellationToken)
        where TMaterializer : struct, IQueryRowMaterializer<TRow>
    {
        var snapshot = contract.Snapshot;
        var dialect = request.Dialect ?? contract.Dialect;
        var recordProgram = SeparatedValuesRecordProgram.CompileQuery(
            contract,
            request.ExecutionContext,
            mapping);
        var recordExecutor = recordProgram.CreateExecutor();
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

            EnsureContractFingerprint(request, contract, cancellationToken);

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
            var inputBufferSize = SelectSequentialInputBufferSize(
                snapshot.Identity.Length,
                request.ExecutionContext.Plan.AcceptedTake is > 0 and <= EarlyTakeRowLimit &&
                !request.ExecutionContext.Plan.AcceptedSkip.HasValue);
            if (snapshot.Identity.Length > inputBufferSize)
                EnsureContractFingerprint(request, contract, cancellationToken);
            reader = new SeparatedValuesUtf8Reader(
                snapshot.Identity.CanonicalPath,
                dialect,
                request.SkipLines,
                inputBufferSize,
                cancellationToken);
            if (snapshot.Identity.Length <= inputBufferSize)
                reader.EnsureBufferedFingerprintMatches(snapshot.Identity);
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

            var processor = new SeparatedValuesProjectedRowProcessor<
                TRow,
                SeparatedValuesQueryRowProjector<TRow, TMaterializer>>(
                contract,
                request.ExecutionContext,
                writer,
                progress,
                chunkSize,
                cancellationToken,
                recordExecutor,
                recordExecutor.CreateQueryProjector<TRow, TMaterializer>(),
                rowNumberOffset,
                skipOverride);
            var completedInput = true;
            while (true)
            {
                SeparatedValuesUtf8Record record;
                try
                {
                    if (!reader.TryRead(out record))
                        break;
                }
                catch (InvalidDataException exception) when
                    (exception.Message.Contains("not valid UTF-8", StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"Separated-values source '{snapshot.Identity.CanonicalPath}' row " +
                        $"{processor.RowsRead + 1:N0} column '<malformed UTF-8 field>' is not valid UTF-8.",
                        exception);
                }

                summaryBuilder?.ObserveRecord(record.StartOffset, record.EndOffset);
                var shouldContinue = dialect.IsStrict && record.Bytes.IndexOf((byte)'"') < 0
                    ? processor.ProcessUnquoted(record, request.SeparatorByte)
                    : processor.Process(record);
                if (shouldContinue)
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

    private static InvalidOperationException QueryShapeMismatch(
        SourceIdentity identity,
        string fingerprint,
        string reason)
    {
        return new InvalidOperationException(
            $"Separated-values source '{identity.SchemaName}.{identity.MethodName}' " +
            $"(context '{identity.SourceContextId}', alias '{identity.Alias}') cannot materialize " +
            $"query shape '{fingerprint}': {reason}.");
    }

    private static void EnsureContractFingerprint(
        SeparatedValuesScanRequest request,
        SeparatedValuesSourceContract contract,
        CancellationToken cancellationToken)
    {
        var currentIdentity = StructuredFileIdentity.Capture(
            request.Path,
            contract.Snapshot.Identity.ParserOptions,
            cancellationToken);
        if (!StructuredFileIdentityComparer.Instance.Equals(contract.Snapshot.Identity, currentIdentity))
        {
            throw new StructuredSchemaDriftException(
                contract.Snapshot.Identity.CanonicalPath,
                "the file identity changed after planning");
        }
    }

    internal static int SelectSequentialInputBufferSize(long fileLength, bool isEarlyTake)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fileLength);
        var maximum = isEarlyTake ? EarlyTakeInputBufferSize : SequentialInputBufferSize;
        var requested = (uint)Math.Clamp(fileLength, MinimumSequentialInputBufferSize, maximum);
        return checked((int)BitOperations.RoundUpToPowerOf2(requested));
    }

    internal static int SelectSequentialQueryChunkSize(long fileLength, int plannedChunkSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fileLength);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(plannedChunkSize);
        return fileLength <= SequentialInputBufferSize
            ? Math.Min(plannedChunkSize, SmallSequentialQueryChunkRows)
            : plannedChunkSize;
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
