using System.Globalization;
using Musoq.DataSources.Common;
using Musoq.DataSources.Structured;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues.Benchmark;

/// <summary>
/// Benchmark-only snapshot of the byte-native object-array path that preceded
/// query-row specialization. It deliberately retains the original full
/// fingerprint pass, fixed sequential buffers, general field traversal, and
/// object-array projection so later production optimizations cannot move the
/// comparison baseline.
/// </summary>
internal sealed class FrozenByteNativeLegacySchema
{
    public ISchema CreateCompiledSchema() => new FrozenCompiledSchema(this);

    public RowSource<object?[]> GetRowSource(
        string path,
        string separator,
        bool hasHeader,
        int skipLines,
        SourceExecutionContext executionContext)
    {
        var dialect = SeparatedValuesPipelineModules.Default.DialectResolver.Resolve(
            separator,
            executionContext.SourceRuntimeSettings);
        return new FrozenByteNativeLegacyRowSource(
            path,
            separator,
            hasHeader,
            skipLines,
            executionContext,
            dialect);
    }

    private sealed class FrozenCompiledSchema(FrozenByteNativeLegacySchema owner) : SeparatedValuesSchema
    {
        public override SourceDescriptor DescribeSource(
            string name,
            SourceDescribeContext context,
            params object?[] parameters)
        {
            return base.DescribeSource(name, context, parameters) with
            {
                TransferCapabilities = SourceTransferCapabilities.None
            };
        }

        public override RowSource<T> GetRowSource<T>(
            string name,
            SourceExecutionContext executionContext,
            params object?[] parameters)
        {
            var sourceParameters = ParseParameters(name, parameters);
            if (typeof(T) != typeof(object[]))
            {
                throw new InvalidOperationException(
                    $"Frozen separated-values legacy rows require '{typeof(object[])}', not '{typeof(T)}'.");
            }

            return (RowSource<T>)(object)owner.GetRowSource(
                sourceParameters.Path,
                sourceParameters.Separator,
                sourceParameters.HasHeader,
                sourceParameters.SkipLines,
                executionContext);
        }

        private static FrozenSourceParameters ParseParameters(string name, object?[] parameters)
        {
            if (string.Equals(name, "delimited", StringComparison.OrdinalIgnoreCase))
            {
                if (parameters is not [string path, string delimiter, bool hasHeader, int skipLines])
                    throw new ArgumentException("The delimited source requires (path, delimiter, hasHeader, skipLines).");
                return new FrozenSourceParameters(path, delimiter, hasHeader, skipLines);
            }

            if (parameters is not [string sourcePath, bool sourceHasHeader, int sourceSkipLines])
                throw new ArgumentException("The separated-values source requires (path, hasHeader, skipLines).");

            var separator = name.ToLowerInvariant() switch
            {
                "comma" => ",",
                "tab" => "\t",
                "semicolon" => ";",
                _ => throw new NotSupportedException($"Frozen separated-values source '{name}' is not supported.")
            };
            return new FrozenSourceParameters(sourcePath, separator, sourceHasHeader, sourceSkipLines);
        }
    }

    private readonly record struct FrozenSourceParameters(
        string Path,
        string Separator,
        bool HasHeader,
        int SkipLines);
}

internal sealed class FrozenByteNativeLegacyRowSource : RowSourceBase<object?[]>
{
    private readonly SeparatedValuesScanRequest _request;

    public FrozenByteNativeLegacyRowSource(
        string path,
        string separator,
        bool hasHeader,
        int skipLines,
        SourceExecutionContext executionContext,
        SeparatedValuesDialect dialect)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(separator);
        ArgumentNullException.ThrowIfNull(executionContext);
        ArgumentNullException.ThrowIfNull(dialect);
        ArgumentOutOfRangeException.ThrowIfNegative(skipLines);
        if (separator.Length != 1 || separator[0] > 0x7f)
            throw new ArgumentException("The separator must be one ASCII character.", nameof(separator));

        _request = new SeparatedValuesScanRequest(
            Path.GetFullPath(path),
            separator,
            checked((byte)separator[0]),
            hasHeader,
            skipLines,
            executionContext,
            dialect);
    }

    protected override void CollectChunks(IChunkWriter<object?[]> writer)
    {
        FrozenByteNativeLegacyScan.Execute(_request, writer);
    }
}

internal static class FrozenByteNativeLegacyScan
{
    private const int EarlyTakeInputBufferSize = 64 * 1024;
    private const int EarlyTakeRowLimit = 4096;
    private const int SequentialInputBufferSize = 1024 * 1024;
    private const int ZeroColumnChunkRows = 1024 * 1024;
    private const string SourceName = "separated_values_frozen_legacy";

    public static void Execute(SeparatedValuesScanRequest request, IChunkWriter<object?[]> writer)
    {
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
            EnsurePlanStillMatches(contract.Snapshot, request.ExecutionContext);
            if (contract.HasExactCardinality)
                progress.RowsKnown(contract.Snapshot.RowCount);

            var readPlan = SeparatedValuesReadPlan.From(request.ExecutionContext.Plan);
            if (request.ExecutionContext.Plan.AcceptedTake is 0)
                return;

            if (contract.HasExactCardinality && CanUseZeroColumnScan(readPlan, request.ExecutionContext))
            {
                progress.RowsRead(contract.Snapshot.RowCount);
                WriteRepeatedRows(writer, contract.Snapshot.RowCount);
                rowsEmitted = contract.Snapshot.RowCount;
                return;
            }

            var projectedColumns = readPlan.ProjectionAccepted
                ? request.ExecutionContext.Plan.AcceptedColumns.Count
                : request.ExecutionContext.AllColumns.Count > 0
                    ? request.ExecutionContext.AllColumns.Count
                    : contract.Snapshot.Columns.Length;
            var strategy = SeparatedValuesReadStrategySelector.Select(new SeparatedValuesReadStrategyContext(
                contract.Snapshot.Identity.Length,
                projectedColumns,
                contract.Snapshot.Columns.Length,
                request.ExecutionContext.Plan.AcceptedTake,
                readPlan.HasResidualWork,
                readPlan.ProjectionAccepted));
            progress.SetRowsReadReportInterval(strategy.RowChunkSize);
            rowsEmitted = ProcessSequential(
                request,
                contract,
                writer,
                progress,
                strategy.RowChunkSize,
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
                cancellationToken,
                useStrictFramingFastPath: false);
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
                cancellationToken,
                useStrictFramingFastPath: false);
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

            var processor = new FrozenByteNativeLegacyRowProcessor(
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

    private static void WriteRepeatedRows(IChunkWriter<object?[]> writer, long rowCount)
    {
        while (rowCount > 0)
        {
            var count = (int)Math.Min(ZeroColumnChunkRows, rowCount);
            writer.Write(new RepeatedValueChunk<object?[]>(Array.Empty<object?>(), count));
            rowCount -= count;
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

internal sealed class FrozenByteNativeLegacyRowProcessor
{
    private readonly FrozenFieldAction[] _actions;
    private readonly CancellationToken _cancellationToken;
    private readonly int _chunkSize;
    private readonly IFormatProvider _culture;
    private readonly FrozenFieldLocation[] _locations;
    private readonly SeparatedValuesPredicateEvaluator _predicate;
    private readonly DataSourceProgressReporter? _progress;
    private readonly FrozenProjectionPlan _projection;
    private readonly long _rowNumberOffset;
    private readonly SeparatedValuesSchemaValidator _schemaValidator;
    private readonly bool _skipBeforeEvaluation;
    private readonly StructuredStringPool _stringPool;
    private readonly long? _take;
    private readonly IChunkWriter<object?[]> _writer;
    private List<object?[]>? _chunk;
    private long _emittedRows;
    private long _skipRemaining;
    private int _zeroColumnRows;

    public FrozenByteNativeLegacyRowProcessor(
        SeparatedValuesSourceContract contract,
        SourceExecutionContext executionContext,
        IChunkWriter<object?[]> writer,
        DataSourceProgressReporter? progress,
        int chunkSize,
        CancellationToken cancellationToken,
        long rowNumberOffset = 0,
        long? skipOverride = null)
    {
        _projection = FrozenProjectionPlan.Create(contract, executionContext);
        _predicate = SeparatedValuesPredicateEvaluator.Create(contract, executionContext.Plan.AcceptedPredicate);
        _schemaValidator = SeparatedValuesSchemaValidator.Create(contract);
        _actions = new FrozenFieldAction[contract.Snapshot.Columns.Length];
        for (var sourceOrdinal = 0; sourceOrdinal < _actions.Length; sourceOrdinal++)
        {
            var validates = _schemaValidator.TryGetValidationConversion(sourceOrdinal, out var validationConversion);
            var parsesPredicate = _predicate.TryGetConversion(sourceOrdinal, out var predicateConversion);
            _actions[sourceOrdinal] = new FrozenFieldAction(
                validates ? validationConversion : parsesPredicate ? predicateConversion : null,
                validates,
                _predicate.HasTermsAt(sourceOrdinal),
                _projection.HasProjectionAt(sourceOrdinal));
        }

        _locations = new FrozenFieldLocation[_projection.BindingCount];
        _stringPool = contract.Snapshot.StringPool;
        _culture = SeparatedValuesValueConverter.GetCulture(contract.Dialect.CultureName);
        _writer = writer;
        _progress = progress;
        _chunkSize = chunkSize;
        _cancellationToken = cancellationToken;
        _rowNumberOffset = rowNumberOffset;
        _skipRemaining = skipOverride ?? executionContext.Plan.AcceptedSkip ?? 0;
        _take = executionContext.Plan.AcceptedTake;
        var readPlan = SeparatedValuesReadPlan.From(executionContext.Plan);
        _skipBeforeEvaluation = executionContext.Plan.AcceptedPredicate is null &&
                                readPlan.AcceptedPredicate is null &&
                                !readPlan.HasResidualWork;
    }

    public long RowsRead { get; private set; }

    public long RowsEmitted => _emittedRows;

    public bool Process(SeparatedValuesUtf8Record record)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        RowsRead++;
        _progress?.RowRead();
        var rowNumber = _rowNumberOffset + RowsRead;

        if (_skipBeforeEvaluation && _skipRemaining > 0)
        {
            _skipRemaining--;
            return true;
        }

        Array.Clear(_locations);
        var predicateTermIndex = 0;
        var predicateMatched = true;
        var bindingIndex = 0;
        var fieldIndex = 0;
        foreach (var field in record)
        {
            ProcessField(
                fieldIndex,
                field,
                rowNumber,
                ref predicateTermIndex,
                ref predicateMatched,
                ref bindingIndex);
            fieldIndex++;
        }

        if (!predicateMatched || !_predicate.IsComplete(predicateTermIndex))
            return true;

        if (!_skipBeforeEvaluation && _skipRemaining > 0)
        {
            _skipRemaining--;
            return true;
        }

        if (_take is not null && _emittedRows >= _take.Value)
            return false;

        if (_projection.HasOutputColumns)
        {
            _chunk ??= new List<object?[]>(_chunkSize);
            _chunk.Add(Materialize(record, rowNumber));
        }
        else
        {
            _zeroColumnRows++;
        }

        _emittedRows++;
        if (_zeroColumnRows == _chunkSize || _chunk?.Count == _chunkSize)
            Flush();
        return _take is null || _emittedRows < _take.Value;
    }

    public void Complete() => Flush();

    private object?[] Materialize(SeparatedValuesUtf8Record record, long rowNumber)
    {
        var output = new object?[_projection.OutputCount];
        for (var bindingIndex = 0; bindingIndex < _projection.BindingCount; bindingIndex++)
        {
            ref readonly var binding = ref _projection.GetBinding(bindingIndex);
            ref readonly var location = ref _locations[bindingIndex];
            if (!location.Present)
                continue;

            var field = location.CreateField(record.Bytes);
            output[binding.OutputOrdinal] = location.Parsed.CanMaterialize(binding.Conversion)
                ? location.Parsed.Materialize(binding.Conversion)
                : binding.Conversion == SeparatedValuesConversion.String &&
                  !SeparatedValuesValueConverter.IsNull(field) &&
                  !field.NeedsUnescaping
                    ? _stringPool.GetOrAddUtf8(binding.SourceOrdinal, field.EncodedValue)
                    : SeparatedValuesValueConverter.Convert(
                        field,
                        binding.Conversion,
                        binding.Name,
                        rowNumber,
                        _culture);
        }

        return output;
    }

    private void ProcessField(
        int fieldIndex,
        SeparatedValuesUtf8Field field,
        long rowNumber,
        ref int predicateTermIndex,
        ref bool predicateMatched,
        ref int bindingIndex)
    {
        if (fieldIndex >= _actions.Length)
            _schemaValidator.ThrowWidthDrift(rowNumber);
        ref readonly var action = ref _actions[fieldIndex];
        var parsed = default(SeparatedValuesParsedValue);
        if (action.ParseConversion.HasValue)
        {
            if (SeparatedValuesValueConverter.IsNull(field))
            {
                parsed = SeparatedValuesParsedValue.Null(action.ParseConversion.Value);
            }
            else if (!SeparatedValuesParsedValue.TryParse(
                         field,
                         action.ParseConversion.Value,
                         _culture,
                         out parsed))
            {
                if (action.ValidatesSampledType)
                    _schemaValidator.ThrowInvalidSampledValue(fieldIndex, field, rowNumber);
                _predicate.ThrowInvalidValue(fieldIndex, field, rowNumber);
            }
        }

        if (predicateMatched && action.HasPredicate)
        {
            predicateMatched = _predicate.EvaluateField(
                fieldIndex,
                field,
                parsed,
                rowNumber,
                ref predicateTermIndex);
        }

        if ((predicateMatched || !_predicate.HasTerms) && action.HasProjection)
            CaptureField(fieldIndex, field, parsed, ref bindingIndex);
    }

    private void CaptureField(
        int fieldIndex,
        SeparatedValuesUtf8Field field,
        SeparatedValuesParsedValue parsed,
        ref int bindingIndex)
    {
        while (bindingIndex < _projection.BindingCount &&
               _projection.GetBinding(bindingIndex).SourceOrdinal < fieldIndex)
            bindingIndex++;

        while (bindingIndex < _projection.BindingCount &&
               _projection.GetBinding(bindingIndex).SourceOrdinal == fieldIndex)
        {
            _locations[bindingIndex] = FrozenFieldLocation.Capture(field, parsed);
            bindingIndex++;
        }
    }

    private void Flush()
    {
        if (_zeroColumnRows > 0)
        {
            _writer.Write(new RepeatedValueChunk<object?[]>(Array.Empty<object?>(), _zeroColumnRows));
            _zeroColumnRows = 0;
        }

        if (_chunk is null || _chunk.Count == 0)
            return;
        _writer.Write(_chunk);
        _chunk = null;
    }

    private readonly record struct FrozenFieldAction(
        SeparatedValuesConversion? ParseConversion,
        bool ValidatesSampledType,
        bool HasPredicate,
        bool HasProjection);
}

internal sealed class FrozenProjectionPlan
{
    private readonly FrozenBoundColumn[] _columns;

    private FrozenProjectionPlan(FrozenBoundColumn[] columns, int outputCount)
    {
        _columns = columns;
        OutputCount = outputCount;
    }

    public int BindingCount => _columns.Length;

    public int OutputCount { get; }

    public bool HasOutputColumns => OutputCount > 0;

    public static FrozenProjectionPlan Create(
        SeparatedValuesSourceContract contract,
        SourceExecutionContext executionContext)
    {
        var snapshot = contract.Snapshot;
        var columns = new List<FrozenBoundColumn>();
        var outputCount = 0;
        var readPlan = SeparatedValuesReadPlan.From(executionContext.Plan);
        var projectionAccepted = readPlan.ProjectionAccepted || executionContext.Plan.AcceptedColumns.Count > 0;

        if (executionContext.Plan.Properties is not null &&
            executionContext.Plan.Properties.TryGetValue(SeparatedValuesPlanning.LayoutPropertyName, out var value) &&
            value is StructuredExecutionLayout layout)
        {
            foreach (var binding in layout.Bindings)
            {
                var schemaColumn = FindSchemaColumn(executionContext.AllColumns, binding.Name);
                var outputType = schemaColumn?.ColumnType ?? contract.ColumnContracts[binding.SourceOrdinal].ClrType;
                var outputOrdinal = schemaColumn?.ColumnIndex ?? binding.OutputOrdinal;
                AddColumn(columns, snapshot, binding.Name, outputOrdinal, outputType);
                outputCount = Math.Max(outputCount, outputOrdinal + 1);
            }
        }
        else if (projectionAccepted)
        {
            var denseFallback = 0;
            foreach (var accepted in executionContext.Plan.AcceptedColumns)
            {
                var name = ResolveName(snapshot, accepted.Name);
                var schemaColumn = FindSchemaColumn(executionContext.AllColumns, name);
                var outputOrdinal = schemaColumn?.ColumnIndex ?? denseFallback;
                var outputType = schemaColumn?.ColumnType ?? GetSnapshotColumn(snapshot, name).ClrType;
                AddColumn(columns, snapshot, name, outputOrdinal, outputType);
                outputCount = Math.Max(outputCount, outputOrdinal + 1);
                denseFallback++;
            }
        }
        else if (executionContext.AllColumns.Count > 0)
        {
            foreach (var schemaColumn in executionContext.AllColumns.OrderBy(static column => column.ColumnIndex))
            {
                var name = ResolveName(snapshot, schemaColumn.ColumnName);
                AddColumn(columns, snapshot, name, schemaColumn.ColumnIndex, schemaColumn.ColumnType);
                outputCount = Math.Max(outputCount, schemaColumn.ColumnIndex + 1);
            }
        }
        else
        {
            foreach (var column in snapshot.Columns)
            {
                AddColumn(columns, snapshot, column.Name, column.SourceOrdinal, column.ClrType);
                outputCount++;
            }
        }

        return new FrozenProjectionPlan(
            columns.OrderBy(static column => column.SourceOrdinal).ToArray(),
            outputCount);
    }

    public bool HasProjectionAt(int sourceOrdinal) =>
        Array.Exists(_columns, column => column.SourceOrdinal == sourceOrdinal);

    public ref readonly FrozenBoundColumn GetBinding(int index) => ref _columns[index];

    private static void AddColumn(
        ICollection<FrozenBoundColumn> columns,
        StructuredSchemaSnapshot snapshot,
        string name,
        int outputOrdinal,
        Type outputType)
    {
        var snapshotColumn = GetSnapshotColumn(snapshot, name);
        columns.Add(new FrozenBoundColumn(
            name,
            snapshotColumn.SourceOrdinal,
            outputOrdinal,
            SeparatedValuesValueConverter.GetConversion(outputType, snapshotColumn.TypeState)));
    }

    private static StructuredColumnSnapshot GetSnapshotColumn(
        StructuredSchemaSnapshot snapshot,
        string name)
    {
        if (snapshot.TryGetColumn(name, out var column))
            return column;
        throw new StructuredUnknownColumnException(name, snapshot.Identity.CanonicalPath);
    }

    private static string ResolveName(StructuredSchemaSnapshot snapshot, string name)
    {
        if (snapshot.TryGetColumn(name, out _))
            return name;
        var dotIndex = name.LastIndexOf('.');
        if (dotIndex >= 0 && snapshot.TryGetColumn(name[(dotIndex + 1)..], out _))
            return name[(dotIndex + 1)..];
        throw new StructuredUnknownColumnException(name, snapshot.Identity.CanonicalPath);
    }

    private static ISchemaColumn? FindSchemaColumn(
        IReadOnlyCollection<ISchemaColumn> columns,
        string name)
    {
        foreach (var column in columns)
        {
            if (string.Equals(column.ColumnName, name, StringComparison.Ordinal) ||
                column.ColumnName.EndsWith('.' + name, StringComparison.Ordinal))
                return column;
        }

        return null;
    }
}

internal readonly record struct FrozenBoundColumn(
    string Name,
    int SourceOrdinal,
    int OutputOrdinal,
    SeparatedValuesConversion Conversion);

internal readonly record struct FrozenFieldLocation(
    int Offset,
    int Length,
    bool WasQuoted,
    bool NeedsUnescaping,
    SeparatedValuesEscapeMode EscapeMode,
    bool IsNullToken,
    byte? Quote,
    SeparatedValuesParsedValue Parsed,
    bool Present)
{
    public static FrozenFieldLocation Capture(
        SeparatedValuesUtf8Field field,
        SeparatedValuesParsedValue parsed)
    {
        return new FrozenFieldLocation(
            field.EncodedOffset,
            field.EncodedValue.Length,
            field.WasQuoted,
            field.NeedsUnescaping,
            field.EscapeMode,
            field.IsNullToken,
            field.Quote,
            parsed,
            true);
    }

    public SeparatedValuesUtf8Field CreateField(ReadOnlySpan<byte> recordBytes)
    {
        return new SeparatedValuesUtf8Field(
            recordBytes.Slice(Offset, Length),
            Offset,
            WasQuoted,
            NeedsUnescaping,
            EscapeMode,
            IsNullToken,
            Quote);
    }
}
