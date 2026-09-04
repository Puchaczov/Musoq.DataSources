#nullable enable

using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Musoq.DataSources.Common;
using Musoq.DataSources.Structured;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Helpers;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues;

internal interface ISeparatedValuesRowProjector<TRow>
{
    bool CanRepeatRow { get; }

    TRow RepeatedRow { get; }

    TRow Materialize(SeparatedValuesUtf8Record record, long rowNumber);
}

internal sealed class SeparatedValuesProjectedRowProcessor<TRow, TProjector>
    where TProjector : struct, ISeparatedValuesRowProjector<TRow>
{
    private readonly CancellationToken _cancellationToken;
    private readonly int _chunkSize;
    private readonly SeparatedValuesRecordKernel _kernel;
    private readonly DataSourceProgressReporter? _progress;
    private readonly long _rowNumberOffset;
    private readonly bool _skipBeforeEvaluation;
    private readonly long? _take;
    private readonly IChunkWriter<TRow> _writer;
    private TProjector _projector;
    private List<TRow>? _chunk;
    private long _emittedRows;
    private long _skipRemaining;
    private int _zeroColumnRows;

    public SeparatedValuesProjectedRowProcessor(
        SeparatedValuesSourceContract contract,
        SourceExecutionContext executionContext,
        IChunkWriter<TRow> writer,
        DataSourceProgressReporter? progress,
        int chunkSize,
        CancellationToken cancellationToken,
        SeparatedValuesRecordKernel kernel,
        TProjector projector,
        long rowNumberOffset = 0,
        long? skipOverride = null,
        bool sliceAlreadyApplied = false)
    {
        ArgumentNullException.ThrowIfNull(contract);
        var snapshot = contract.Snapshot;
        _writer = writer;
        _progress = progress;
        _chunkSize = chunkSize;
        _cancellationToken = cancellationToken;
        _rowNumberOffset = rowNumberOffset;
        _projector = projector;
        _skipRemaining = sliceAlreadyApplied ? 0 : skipOverride ?? executionContext.Plan.AcceptedSkip ?? 0;
        _take = sliceAlreadyApplied ? null : executionContext.Plan.AcceptedTake;
        var readPlan = SeparatedValuesReadPlan.From(executionContext.Plan);
        _skipBeforeEvaluation = !sliceAlreadyApplied &&
                                executionContext.Plan.AcceptedPredicate is null &&
                                readPlan.AcceptedPredicate is null &&
                                !readPlan.HasResidualWork;
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
    }

    public long RowsRead { get; private set; }

    public long RowsEmitted => _emittedRows;

    public bool HasWork => _take is null || _take.Value > 0;

    public bool Process(SeparatedValuesUtf8Record record)
    {
        return ProcessCore(record, false, default);
    }

    public bool ProcessUnquoted(SeparatedValuesUtf8Record record, byte separator)
    {
        return ProcessCore(record, true, separator);
    }

    private bool ProcessCore(
        SeparatedValuesUtf8Record record,
        bool unquoted,
        byte separator)
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

        var accepted = unquoted
            ? _kernel.PrepareUnquoted(record, separator, rowNumber)
            : _kernel.Prepare(record, rowNumber);
        if (!accepted)
            return true;

        if (!_skipBeforeEvaluation && _skipRemaining > 0)
        {
            _skipRemaining--;
            return true;
        }

        if (_take is not null && _emittedRows >= _take.Value)
            return false;

        if (!_projector.CanRepeatRow)
        {
            _chunk ??= new List<TRow>(_chunkSize);
            _chunk.Add(_projector.Materialize(record, rowNumber));
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

    public void Complete()
    {
        Flush();
    }

    private void Flush()
    {
        if (_zeroColumnRows > 0)
        {
            _writer.Write(new RepeatedValueChunk<TRow>(_projector.RepeatedRow, _zeroColumnRows));
            _zeroColumnRows = 0;
        }

        if (_chunk is null || _chunk.Count == 0)
            return;

        _writer.Write(_chunk);
        _chunk = null;
    }
}

internal sealed class SeparatedValuesRecordProgram
{
    private readonly SeparatedValuesFieldAction[] _actions;
    private readonly int[] _captureSourceOrdinals;
    private readonly IFormatProvider _culture;
    private readonly SeparatedValuesPredicateEvaluator _predicate;
    private readonly SeparatedValuesQueryProjectionPlan _queryProjection;
    private readonly SeparatedValuesSchemaValidator _schemaValidator;
    private readonly StructuredStringPool _stringPool;

    private SeparatedValuesRecordProgram(
        SeparatedValuesFieldAction[] actions,
        int[] captureSourceOrdinals,
        StructuredStringPool stringPool,
        IFormatProvider culture,
        SeparatedValuesQueryProjectionPlan queryProjection,
        SeparatedValuesPredicateEvaluator predicate,
        SeparatedValuesSchemaValidator schemaValidator)
    {
        _actions = actions;
        _captureSourceOrdinals = captureSourceOrdinals;
        _stringPool = stringPool;
        _culture = culture;
        _queryProjection = queryProjection;
        _predicate = predicate;
        _schemaValidator = schemaValidator;
    }

    public static SeparatedValuesRecordProgram CompileQuery(
        SeparatedValuesSourceContract contract,
        SourceExecutionContext executionContext,
        SeparatedValuesQueryShapeMapping mapping)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(executionContext);
        ArgumentNullException.ThrowIfNull(mapping);
        var projection = SeparatedValuesQueryProjectionPlan.Create(contract, mapping);
        var predicate = SeparatedValuesPredicateEvaluator.Create(
            contract,
            executionContext.Plan.AcceptedPredicate);
        var validator = SeparatedValuesSchemaValidator.Create(contract);
        var actions = new SeparatedValuesFieldAction[contract.Snapshot.Columns.Length];
        for (var sourceOrdinal = 0; sourceOrdinal < actions.Length; sourceOrdinal++)
        {
            var validatesSampledType = validator.TryGetValidationConversion(sourceOrdinal, out var validationConversion);
            var parsesPredicate = predicate.TryGetConversion(sourceOrdinal, out var predicateConversion);
            var enumPlan = contract.ColumnContracts[sourceOrdinal].EnumPlan;
            var parsesEnum = enumPlan is not null &&
                             (projection.HasProjectionAt(sourceOrdinal) || predicate.HasTermsAt(sourceOrdinal));
            var parseConversion = parsesEnum
                ? enumPlan!.PrimitiveConversion
                : validatesSampledType
                ? validationConversion
                : parsesPredicate
                    ? predicateConversion
                    : (SeparatedValuesConversion?)null;
            actions[sourceOrdinal] = new SeparatedValuesFieldAction(
                parseConversion,
                parsesEnum ? enumPlan : null,
                validatesSampledType,
                predicate.HasTermsAt(sourceOrdinal),
                projection.HasProjectionAt(sourceOrdinal));
        }

        return new SeparatedValuesRecordProgram(
            actions,
            projection.SourceOrdinals,
            contract.Snapshot.StringPool,
            SeparatedValuesValueConverter.GetCulture(contract.Dialect.CultureName),
            projection,
            predicate,
            validator);
    }

    public SeparatedValuesRecordKernel CreateExecutor()
    {
        return new SeparatedValuesRecordKernel(
            _actions,
            new SeparatedValuesPhysicalFieldTraversal(
                _captureSourceOrdinals,
                _stringPool,
                _culture),
            _queryProjection,
            _predicate,
            _schemaValidator);
    }
}

internal readonly record struct SeparatedValuesFieldAction(
    SeparatedValuesConversion? ParseConversion,
    SeparatedValuesEnumPlan? EnumPlan,
    bool ValidatesSampledType,
    bool HasPredicate,
    bool HasProjection);

internal sealed class SeparatedValuesRecordKernel
{
    private readonly SeparatedValuesFieldAction[] _actions;
    private readonly SeparatedValuesPhysicalFieldTraversal _fields;
    private readonly SeparatedValuesPredicateEvaluator _predicate;
    private readonly SeparatedValuesQueryProjectionPlan _queryProjection;
    private readonly SeparatedValuesSchemaValidator _schemaValidator;

    internal SeparatedValuesRecordKernel(
        SeparatedValuesFieldAction[] actions,
        SeparatedValuesPhysicalFieldTraversal fields,
        SeparatedValuesQueryProjectionPlan queryProjection,
        SeparatedValuesPredicateEvaluator predicate,
        SeparatedValuesSchemaValidator schemaValidator)
    {
        _actions = actions;
        _fields = fields;
        _queryProjection = queryProjection;
        _predicate = predicate;
        _schemaValidator = schemaValidator;
    }

    public bool HasOutputColumns => _queryProjection.HasOutputColumns;

    public long MaterializedRowCount => _fields.MaterializedRowCount;

    public long FieldsVisited { get; private set; }

    public long ParsedFields { get; private set; }

    public SeparatedValuesQueryRowProjector<TRow, TMaterializer> CreateQueryProjector<TRow, TMaterializer>()
        where TMaterializer : struct, IQueryRowMaterializer<TRow>
    {
        return new SeparatedValuesQueryRowProjector<TRow, TMaterializer>(_fields, _queryProjection);
    }

    public bool Prepare(SeparatedValuesUtf8Record record, long rowNumber)
    {
        _fields.BeginRecord();
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

        return predicateMatched && _predicate.IsComplete(predicateTermIndex);
    }

    public bool PrepareUnquoted(
        SeparatedValuesUtf8Record record,
        byte separator,
        long rowNumber)
    {
        _fields.BeginRecord();
        var predicateTermIndex = 0;
        var predicateMatched = true;
        var bindingIndex = 0;
        var fieldIndex = 0;
        var fieldStart = 0;
        var bytes = record.Bytes;

        while (true)
        {
            var relativeSeparator = bytes[fieldStart..].IndexOf(separator);
            var fieldEnd = relativeSeparator < 0
                ? bytes.Length
                : fieldStart + relativeSeparator;
            var field = new SeparatedValuesUtf8Field(
                bytes[fieldStart..fieldEnd],
                fieldStart,
                false,
                false);
            ProcessField(
                fieldIndex,
                field,
                rowNumber,
                ref predicateTermIndex,
                ref predicateMatched,
                ref bindingIndex);
            fieldIndex++;

            if (relativeSeparator < 0)
                break;
            fieldStart = fieldEnd + 1;
        }

        return predicateMatched && _predicate.IsComplete(predicateTermIndex);
    }

    private void ProcessField(
        int fieldIndex,
        SeparatedValuesUtf8Field field,
        long rowNumber,
        ref int predicateTermIndex,
        ref bool predicateMatched,
        ref int bindingIndex)
    {
        FieldsVisited++;
        if (fieldIndex >= _actions.Length)
            _schemaValidator.ThrowWidthDrift(rowNumber);
        ref readonly var action = ref _actions[fieldIndex];
        var parsed = default(SeparatedValuesParsedValue);
        if (action.ParseConversion.HasValue && (predicateMatched || action.EnumPlan is null))
        {
            if (SeparatedValuesValueConverter.IsNull(field))
            {
                parsed = SeparatedValuesParsedValue.Null(action.ParseConversion.Value);
            }
            else if (action.EnumPlan is not null)
            {
                if (!action.EnumPlan.TryDecode(field, out parsed))
                    _schemaValidator.ThrowInvalidEnumValue(fieldIndex, field, rowNumber, action.EnumPlan);
            }
            else if (!SeparatedValuesParsedValue.TryParse(
                         field,
                         action.ParseConversion.Value,
                         _fields.Culture,
                         out parsed))
            {
                if (action.ValidatesSampledType)
                    _schemaValidator.ThrowInvalidSampledValue(fieldIndex, field, rowNumber);
                _predicate.ThrowInvalidValue(fieldIndex, field, rowNumber);
            }

            ParsedFields++;
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
            _fields.CaptureField(fieldIndex, field, parsed, ref bindingIndex);
    }

}

internal sealed class SeparatedValuesSchemaValidator
{
    private readonly StructuredColumnSnapshot[] _columns;
    private readonly string _path;
    private readonly bool _validateSampledValues;

    private SeparatedValuesSchemaValidator(
        SeparatedValuesSourceContract? contract,
        StructuredColumnSnapshot[] columns,
        string path,
        bool validateSampledValues)
    {
        Contract = contract;
        _columns = columns;
        _path = path;
        _validateSampledValues = validateSampledValues;
    }

    private SeparatedValuesSourceContract? Contract { get; }

    public static SeparatedValuesSchemaValidator Create(SeparatedValuesSourceContract contract)
    {
        return new SeparatedValuesSchemaValidator(
            contract,
            contract.Snapshot.Columns.ToArray(),
            contract.Snapshot.Identity.CanonicalPath,
            contract.Mode == SeparatedValuesSchemaResolutionMode.Sampled);
    }

    public bool TryGetValidationConversion(int fieldIndex, out SeparatedValuesConversion conversion)
    {
        if (!_validateSampledValues)
        {
            conversion = default;
            return false;
        }

        conversion = _columns[fieldIndex].TypeState.Kind switch
        {
            StructuredValueKind.Boolean => SeparatedValuesConversion.Boolean,
            StructuredValueKind.Long => SeparatedValuesConversion.Int64,
            StructuredValueKind.Decimal => SeparatedValuesConversion.Decimal,
            StructuredValueKind.Double => SeparatedValuesConversion.Double,
            _ => default
        };
        return _columns[fieldIndex].TypeState.Kind is
            StructuredValueKind.Boolean or
            StructuredValueKind.Long or
            StructuredValueKind.Decimal or
            StructuredValueKind.Double;
    }

    public void ThrowWidthDrift(long rowNumber)
    {
        var expectedWidth = Contract?.Snapshot.Columns.Length ?? 0;
        throw new StructuredSchemaDriftException(
            _path,
            $"row {rowNumber:N0} contains more than the bound {expectedWidth:N0} columns");
    }

    public void ThrowInvalidSampledValue(
        int fieldIndex,
        SeparatedValuesUtf8Field field,
        long rowNumber)
    {
        var column = _columns[fieldIndex];
        var observed = field.Decode();
        if (observed.Length > 96)
            observed = observed[..96] + "...";
        throw new FormatException(
            $"Separated-values source '{_path}' row {rowNumber:N0} column '{column.Name}' " +
            $"expected {column.TypeState.Kind} but observed '{observed}'.");
    }

    public void ThrowInvalidEnumValue(
        int fieldIndex,
        SeparatedValuesUtf8Field field,
        long rowNumber,
        SeparatedValuesEnumPlan plan)
    {
        var columnName = (uint)fieldIndex < (uint)_columns.Length
            ? _columns[fieldIndex].Name
            : plan.Descriptor.DisplayName;
        var observed = field.Decode();
        if (observed.Length > 96)
            observed = observed[..96] + "...";
        throw new FormatException(
            $"Separated-values source '{_path}' row {rowNumber:N0} column '{columnName}' " +
            $"cannot be converted as enum '{plan.Descriptor.DisplayName}' ({plan.BackingKind}); " +
            $"observed '{observed}'.");
    }
}

internal sealed class SeparatedValuesPhysicalFieldTraversal
{
    private readonly int[] _sourceOrdinals;
    private readonly SeparatedValuesFieldLocation[] _locations;

    public SeparatedValuesPhysicalFieldTraversal(
        int[] sourceOrdinals,
        StructuredStringPool stringPool,
        IFormatProvider culture)
    {
        _sourceOrdinals = sourceOrdinals ?? throw new ArgumentNullException(nameof(sourceOrdinals));
        _locations = new SeparatedValuesFieldLocation[sourceOrdinals.Length];
        StringPool = stringPool ?? throw new ArgumentNullException(nameof(stringPool));
        Culture = culture ?? throw new ArgumentNullException(nameof(culture));
    }

    public int BindingCount => _sourceOrdinals.Length;

    public StructuredStringPool StringPool { get; }

    public IFormatProvider Culture { get; }

    public long MaterializedRowCount { get; private set; }

    public void BeginRecord()
    {
        Array.Clear(_locations);
    }

    public void CaptureField(
        int fieldIndex,
        SeparatedValuesUtf8Field field,
        SeparatedValuesParsedValue parsed,
        ref int bindingIndex)
    {
        while (bindingIndex < _sourceOrdinals.Length && _sourceOrdinals[bindingIndex] < fieldIndex)
            bindingIndex++;

        while (bindingIndex < _sourceOrdinals.Length && _sourceOrdinals[bindingIndex] == fieldIndex)
        {
            _locations[bindingIndex] = SeparatedValuesFieldLocation.Capture(field, parsed);
            bindingIndex++;
        }
    }

    public ref readonly SeparatedValuesFieldLocation GetLocation(int bindingIndex)
    {
        return ref _locations[bindingIndex];
    }

    public void RecordMaterializedRow()
    {
        MaterializedRowCount++;
    }
}

internal readonly record struct SeparatedValuesFieldLocation(
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
    public bool IsNull => !WasQuoted && (Length == 0 || IsNullToken);

    public static SeparatedValuesFieldLocation Capture(
        SeparatedValuesUtf8Field field,
        SeparatedValuesParsedValue parsed)
    {
        return new SeparatedValuesFieldLocation(
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

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> GetEncodedValue(ReadOnlySpan<byte> recordBytes)
    {
        return recordBytes.Slice(Offset, Length);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public SeparatedValuesUtf8Field CreateField(ReadOnlySpan<byte> recordBytes)
    {
        return new SeparatedValuesUtf8Field(
            GetEncodedValue(recordBytes),
            Offset,
            WasQuoted,
            NeedsUnescaping,
            EscapeMode,
            IsNullToken,
            Quote);
    }
}

internal sealed class SeparatedValuesPredicateEvaluator
{
    private static readonly SeparatedValuesPredicateEvaluator Empty = new([], string.Empty);
    private readonly string _path;
    private readonly PredicateTerm[] _terms;
    private readonly IFormatProvider _culture;

    private SeparatedValuesPredicateEvaluator(
        PredicateTerm[] terms,
        string path,
        IFormatProvider? culture = null)
    {
        _terms = terms;
        _path = path;
        _culture = culture ?? CultureInfo.InvariantCulture;
    }

    public static SeparatedValuesPredicateEvaluator Create(
        SeparatedValuesSourceContract contract,
        SourcePredicateExpression? predicate)
    {
        var snapshot = contract.Snapshot;
        if (predicate is null)
            return Empty;

        var terms = new List<PredicateTerm>();
        AddTerms(contract, predicate, terms);
        terms.Sort((left, right) => left.SourceOrdinal.CompareTo(right.SourceOrdinal));
        return new SeparatedValuesPredicateEvaluator(
            terms.ToArray(),
            snapshot.Identity.CanonicalPath,
            SeparatedValuesValueConverter.GetCulture(contract.Dialect.CultureName));
    }

    public bool HasTerms => _terms.Length > 0;

    public bool HasTermsAt(int sourceOrdinal)
    {
        return Array.Exists(_terms, term => term.SourceOrdinal == sourceOrdinal);
    }

    public bool TryGetConversion(int sourceOrdinal, out SeparatedValuesConversion conversion)
    {
        foreach (var term in _terms)
        {
            if (term.SourceOrdinal != sourceOrdinal)
                continue;
            conversion = term.Conversion;
            return conversion is
                SeparatedValuesConversion.Boolean or
                SeparatedValuesConversion.Byte or
                SeparatedValuesConversion.Decimal or
                SeparatedValuesConversion.Double or
                SeparatedValuesConversion.Int16 or
                SeparatedValuesConversion.Int32 or
                SeparatedValuesConversion.Int64 or
                SeparatedValuesConversion.SByte or
                SeparatedValuesConversion.Single or
                SeparatedValuesConversion.UInt16 or
                SeparatedValuesConversion.UInt32 or
                SeparatedValuesConversion.UInt64;
        }

        conversion = default;
        return false;
    }

    public void ThrowInvalidValue(
        int sourceOrdinal,
        SeparatedValuesUtf8Field field,
        long rowNumber)
    {
        foreach (var term in _terms)
        {
            if (term.SourceOrdinal == sourceOrdinal)
                throw term.InvalidPredicateValue(field, rowNumber, _path);
        }

        throw new InvalidOperationException("A field parse was requested without a predicate term.");
    }

    public bool EvaluateField(
        int fieldIndex,
        SeparatedValuesUtf8Field field,
        SeparatedValuesParsedValue parsed,
        long rowNumber,
        ref int termIndex)
    {
        if (termIndex == _terms.Length)
            return true;

        if (_terms[termIndex].SourceOrdinal < fieldIndex)
            return false;
        if (_terms[termIndex].SourceOrdinal > fieldIndex)
            return true;

        while (termIndex < _terms.Length && _terms[termIndex].SourceOrdinal == fieldIndex)
        {
            if (!_terms[termIndex].Evaluate(field, parsed, rowNumber, _path, _culture))
                return false;
            termIndex++;
        }

        return true;
    }

    public bool IsComplete(int termIndex)
    {
        return termIndex == _terms.Length;
    }

    private static void AddTerms(
        SeparatedValuesSourceContract contract,
        SourcePredicateExpression predicate,
        List<PredicateTerm> terms)
    {
        switch (predicate)
        {
            case SourcePredicateLogical { Operator: SourcePredicateLogicalOperator.And } logical:
                AddTerms(contract, logical.Left, terms);
                AddTerms(contract, logical.Right, terms);
                return;
            case SourcePredicateComparison comparison when
                SeparatedValuesSourcePlanner.TryGetEnumComparisonParts(
                    comparison,
                    out var enumComparisonName,
                    out var enumLiteral,
                    out var enumComparisonOperator):
            {
                if (!SeparatedValuesSourcePlanner.TryGetEnumColumn(
                        contract,
                        enumComparisonName,
                        out var enumColumn,
                        out _,
                        out var enumPlan))
                    throw new StructuredUnknownColumnException(
                        enumComparisonName,
                        contract.Snapshot.Identity.CanonicalPath);

                terms.Add(PredicateTerm.CreateEnumComparison(
                    enumColumn,
                    enumLiteral.Value,
                    enumComparisonOperator,
                    enumPlan));
                return;
            }
            case SourcePredicateIn membership when
                SeparatedValuesSourcePlanner.TryGetEnumColumn(
                    contract,
                    membership.Expression,
                    out var enumMembershipColumn,
                    out _,
                    out var enumMembershipPlan):
            {
                var values = new List<EnumScalarValue>(membership.Values.Count);
                foreach (var value in membership.Values)
                {
                    if (value is not SourcePredicateEnumLiteral literal)
                        throw new InvalidOperationException(
                            "Separated-values execution received a non-enum membership literal.");

                    var duplicate = false;
                    foreach (var existing in values)
                    {
                        if (existing == literal.Value)
                        {
                            duplicate = true;
                            break;
                        }
                    }

                    if (!duplicate)
                        values.Add(literal.Value);
                }

                var normalized = values.ToArray();
                for (var index = 1; index < normalized.Length; index++)
                {
                    var value = normalized[index];
                    var position = index - 1;
                    while (position >= 0 && normalized[position].RawValue > value.RawValue)
                    {
                        normalized[position + 1] = normalized[position];
                        position--;
                    }

                    normalized[position + 1] = value;
                }

                terms.Add(PredicateTerm.CreateEnumMembership(
                    enumMembershipColumn,
                    normalized,
                    membership.IsNegated,
                    enumMembershipPlan));
                return;
            }
            case SourcePredicateNullCheck nullCheck when
                SeparatedValuesSourcePlanner.TryGetEnumColumn(
                    contract,
                    nullCheck.Expression,
                    out var enumNullColumn,
                    out _,
                    out var enumNullPlan):
                terms.Add(PredicateTerm.CreateEnumNullCheck(enumNullColumn, nullCheck.IsNegated, enumNullPlan));
                return;
            case SourcePredicateFlags flags when
                SeparatedValuesSourcePlanner.TryGetEnumColumn(
                    contract,
                    flags.Expression,
                    out var enumFlagsColumn,
                    out _,
                    out var enumFlagsPlan):
                terms.Add(PredicateTerm.CreateEnumFlags(
                    enumFlagsColumn,
                    flags.Mask.Value,
                    flags.MatchMode,
                    enumFlagsPlan));
                return;
            case SourcePredicateComparison ordinaryComparison when
                SeparatedValuesSourcePlanner.TryGetComparisonParts(
                    ordinaryComparison,
                    out var name,
                    out var literal,
                    out var op):
            {
                if (!contract.Snapshot.TryGetColumn(name, out var column))
                    throw new StructuredUnknownColumnException(name, contract.Snapshot.Identity.CanonicalPath);

                var exactType = contract.Mode == SeparatedValuesSchemaResolutionMode.Declared &&
                                contract.ColumnContracts.Length > column.SourceOrdinal
                    ? contract.ColumnContracts[column.SourceOrdinal].ClrType
                    : null;
                terms.Add(PredicateTerm.Create(column, literal.Value!, op, exactType));
                return;
            }
            default:
                throw new InvalidOperationException("Separated-values execution received a predicate it did not accept.");
        }
    }

    private sealed class PredicateTerm
    {
        private enum PredicateTermKind : byte
        {
            Comparison,
            EnumComparison,
            EnumMembership,
            EnumNullCheck,
            EnumFlags
        }

        private readonly bool _boolean;
        private readonly decimal _decimal;
        private readonly double _double;
        private readonly EnumScalarValue[]? _enumValues;
        private readonly EnumScalarValue _enumValue;
        private readonly SeparatedValuesEnumPlan? _enumPlan;
        private readonly bool _isNegated;
        private readonly SourcePredicateFlagsMatchMode _flagsMatchMode;
        private readonly long _long;
        private readonly ulong _ulong;
        private readonly string _name;
        private readonly SourcePredicateComparisonOperator _operator;
        private readonly byte[]? _stringUtf8;
        private readonly StructuredValueKind _type;
        private readonly SeparatedValuesConversion _conversion;
        private readonly PredicateTermKind _kind;

        private PredicateTerm(
            int sourceOrdinal,
            string name,
            StructuredValueKind type,
            SourcePredicateComparisonOperator op,
            long longValue,
            ulong unsignedValue,
            decimal decimalValue,
            double doubleValue,
            bool booleanValue,
            byte[]? stringUtf8,
            SeparatedValuesConversion conversion,
            PredicateTermKind kind = PredicateTermKind.Comparison,
            SeparatedValuesEnumPlan? enumPlan = null,
            EnumScalarValue enumValue = default,
            EnumScalarValue[]? enumValues = null,
            bool isNegated = false,
            SourcePredicateFlagsMatchMode flagsMatchMode = SourcePredicateFlagsMatchMode.Any)
        {
            SourceOrdinal = sourceOrdinal;
            _name = name;
            _type = type;
            _operator = op;
            _long = longValue;
            _ulong = unsignedValue;
            _decimal = decimalValue;
            _double = doubleValue;
            _boolean = booleanValue;
            _stringUtf8 = stringUtf8;
            _conversion = conversion;
            _kind = kind;
            _enumPlan = enumPlan;
            _enumValue = enumValue;
            _enumValues = enumValues;
            _isNegated = isNegated;
            _flagsMatchMode = flagsMatchMode;
        }

        public int SourceOrdinal { get; }

        public SeparatedValuesConversion Conversion => _conversion;

        public static PredicateTerm Create(
            StructuredColumnSnapshot column,
            object literal,
            SourcePredicateComparisonOperator op,
            Type? exactType)
        {
            var conversion = exactType is not null
                ? SeparatedValuesValueConverter.GetConversion(exactType, column.TypeState)
                : column.TypeState.Kind switch
                {
                    StructuredValueKind.Long => SeparatedValuesConversion.Int64,
                    StructuredValueKind.Decimal => SeparatedValuesConversion.Decimal,
                    StructuredValueKind.Double => SeparatedValuesConversion.Double,
                    StructuredValueKind.Boolean => SeparatedValuesConversion.Boolean,
                    StructuredValueKind.String => SeparatedValuesConversion.String,
                    _ => throw new InvalidOperationException(
                        $"Separated-values predicate type '{column.TypeState.Kind}' is not supported.")
                };

            return conversion switch
            {
                SeparatedValuesConversion.Byte or
                    SeparatedValuesConversion.SByte or
                    SeparatedValuesConversion.Int16 or
                    SeparatedValuesConversion.Int32 or
                    SeparatedValuesConversion.Int64 => new PredicateTerm(
                    column.SourceOrdinal,
                    column.Name,
                    column.TypeState.Kind,
                    op,
                    Convert.ToInt64(literal, CultureInfo.InvariantCulture),
                    0,
                    0,
                    0,
                    false,
                    null,
                    conversion),
                SeparatedValuesConversion.UInt16 or
                    SeparatedValuesConversion.UInt32 or
                    SeparatedValuesConversion.UInt64 => new PredicateTerm(
                    column.SourceOrdinal,
                    column.Name,
                    column.TypeState.Kind,
                    op,
                    0,
                    Convert.ToUInt64(literal, CultureInfo.InvariantCulture),
                    0,
                    0,
                    false,
                    null,
                    conversion),
                SeparatedValuesConversion.Decimal => new PredicateTerm(
                    column.SourceOrdinal,
                    column.Name,
                    column.TypeState.Kind,
                    op,
                    0,
                    0,
                    Convert.ToDecimal(literal, CultureInfo.InvariantCulture),
                    0,
                    false,
                    null,
                    conversion),
                SeparatedValuesConversion.Single or SeparatedValuesConversion.Double => new PredicateTerm(
                    column.SourceOrdinal,
                    column.Name,
                    column.TypeState.Kind,
                    op,
                    0,
                    0,
                    0,
                    Convert.ToDouble(literal, CultureInfo.InvariantCulture),
                    false,
                    null,
                    conversion),
                SeparatedValuesConversion.Boolean => new PredicateTerm(
                    column.SourceOrdinal,
                    column.Name,
                    column.TypeState.Kind,
                    op,
                    0,
                    0,
                    0,
                    0,
                    (bool)literal,
                    null,
                    conversion),
                SeparatedValuesConversion.String or
                    SeparatedValuesConversion.Character or
                    SeparatedValuesConversion.DateTime or
                    SeparatedValuesConversion.DateTimeOffset or
                    SeparatedValuesConversion.DateOnly or
                    SeparatedValuesConversion.TimeOnly or
                    SeparatedValuesConversion.TimeSpan or
                    SeparatedValuesConversion.Guid => new PredicateTerm(
                    column.SourceOrdinal,
                    column.Name,
                    column.TypeState.Kind,
                    op,
                    0,
                    0,
                    0,
                    0,
                    false,
                    Encoding.UTF8.GetBytes(Convert.ToString(literal, CultureInfo.InvariantCulture) ?? string.Empty),
                    conversion),
                _ => throw new InvalidOperationException(
                    $"Separated-values predicate type '{conversion}' is not supported.")
            };
        }

        public static PredicateTerm CreateEnumComparison(
            StructuredColumnSnapshot column,
            EnumScalarValue value,
            SourcePredicateComparisonOperator op,
            SeparatedValuesEnumPlan plan)
        {
            return new PredicateTerm(
                column.SourceOrdinal,
                column.Name,
                column.TypeState.Kind,
                op,
                0,
                0,
                0,
                0,
                false,
                null,
                plan.PrimitiveConversion,
                PredicateTermKind.EnumComparison,
                plan,
                value);
        }

        public static PredicateTerm CreateEnumMembership(
            StructuredColumnSnapshot column,
            EnumScalarValue[] values,
            bool isNegated,
            SeparatedValuesEnumPlan plan)
        {
            return new PredicateTerm(
                column.SourceOrdinal,
                column.Name,
                column.TypeState.Kind,
                SourcePredicateComparisonOperator.Equal,
                0,
                0,
                0,
                0,
                false,
                null,
                plan.PrimitiveConversion,
                PredicateTermKind.EnumMembership,
                plan,
                enumValues: values,
                isNegated: isNegated);
        }

        public static PredicateTerm CreateEnumNullCheck(
            StructuredColumnSnapshot column,
            bool isNegated,
            SeparatedValuesEnumPlan plan)
        {
            return new PredicateTerm(
                column.SourceOrdinal,
                column.Name,
                column.TypeState.Kind,
                SourcePredicateComparisonOperator.Equal,
                0,
                0,
                0,
                0,
                false,
                null,
                plan.PrimitiveConversion,
                PredicateTermKind.EnumNullCheck,
                plan,
                isNegated: isNegated);
        }

        public static PredicateTerm CreateEnumFlags(
            StructuredColumnSnapshot column,
            EnumScalarValue mask,
            SourcePredicateFlagsMatchMode matchMode,
            SeparatedValuesEnumPlan plan)
        {
            return new PredicateTerm(
                column.SourceOrdinal,
                column.Name,
                column.TypeState.Kind,
                SourcePredicateComparisonOperator.Equal,
                0,
                0,
                0,
                0,
                false,
                null,
                plan.PrimitiveConversion,
                PredicateTermKind.EnumFlags,
                plan,
                enumValue: mask,
                flagsMatchMode: matchMode);
        }

        public bool Evaluate(
            SeparatedValuesUtf8Field field,
            SeparatedValuesParsedValue parsed,
            long rowNumber,
            string path,
            IFormatProvider culture)
        {
            if (_kind is PredicateTermKind.EnumComparison or
                PredicateTermKind.EnumMembership or
                PredicateTermKind.EnumNullCheck or
                PredicateTermKind.EnumFlags)
                return EvaluateEnum(field, parsed, rowNumber, path);

            if (SeparatedValuesValueConverter.IsNull(field))
                return false;

            if (parsed.CanCompare(_conversion))
            {
                return _conversion switch
                {
                    SeparatedValuesConversion.Byte => Matches(parsed.Byte.CompareTo((byte)_long), _operator),
                    SeparatedValuesConversion.SByte => Matches(parsed.SByte.CompareTo((sbyte)_long), _operator),
                    SeparatedValuesConversion.Int16 => Matches(parsed.Int16.CompareTo((short)_long), _operator),
                    SeparatedValuesConversion.Int32 => Matches(parsed.Int32.CompareTo((int)_long), _operator),
                    SeparatedValuesConversion.Int64 => Matches(parsed.Int64.CompareTo(_long), _operator),
                    SeparatedValuesConversion.UInt16 => Matches(parsed.UInt16.CompareTo((ushort)_ulong), _operator),
                    SeparatedValuesConversion.UInt32 => Matches(parsed.UInt32.CompareTo((uint)_ulong), _operator),
                    SeparatedValuesConversion.UInt64 => Matches(parsed.UInt64.CompareTo(_ulong), _operator),
                    SeparatedValuesConversion.Decimal => Matches(decimal.Compare(parsed.Decimal, _decimal), _operator),
                    SeparatedValuesConversion.Single => Matches(parsed.Single.CompareTo((float)_double), _operator),
                    SeparatedValuesConversion.Double => Matches(parsed.Double.CompareTo(_double), _operator),
                    SeparatedValuesConversion.Boolean =>
                        _operator == SourcePredicateComparisonOperator.Equal
                            ? parsed.Boolean == _boolean
                            : parsed.Boolean != _boolean,
                    _ => throw new InvalidOperationException("Unsupported parsed predicate conversion.")
                };
            }

            return _conversion switch
            {
                SeparatedValuesConversion.Byte => EvaluateByte(field, rowNumber, path),
                SeparatedValuesConversion.SByte => EvaluateSByte(field, rowNumber, path),
                SeparatedValuesConversion.Int16 => EvaluateInt16(field, rowNumber, path),
                SeparatedValuesConversion.Int32 => EvaluateInt32(field, rowNumber, path),
                SeparatedValuesConversion.Int64 => EvaluateLong(field, rowNumber, path),
                SeparatedValuesConversion.UInt16 => EvaluateUInt16(field, rowNumber, path),
                SeparatedValuesConversion.UInt32 => EvaluateUInt32(field, rowNumber, path),
                SeparatedValuesConversion.UInt64 => EvaluateULong(field, rowNumber, path),
                SeparatedValuesConversion.Decimal => EvaluateDecimal(field, rowNumber, path, culture),
                SeparatedValuesConversion.Single or SeparatedValuesConversion.Double =>
                    EvaluateDouble(field, rowNumber, path),
                SeparatedValuesConversion.Boolean => EvaluateBoolean(field, rowNumber, path),
                _ => _operator == SourcePredicateComparisonOperator.Equal
                    ? field.ValueEquals(_stringUtf8!)
                    : !field.ValueEquals(_stringUtf8!),
            };
        }

        private bool EvaluateEnum(
            SeparatedValuesUtf8Field field,
            SeparatedValuesParsedValue parsed,
            long rowNumber,
            string path)
        {
            var isNull = SeparatedValuesValueConverter.IsNull(field) || parsed.IsNull;
            if (_kind == PredicateTermKind.EnumNullCheck)
                return _isNegated ? !isNull : isNull;

            if (isNull)
                return false;

            if (!parsed.CanCompare(_conversion))
                throw InvalidPredicateValue(field, rowNumber, path);

            var rawValue = GetEnumRawValue(parsed, _conversion);
            return _kind switch
            {
                PredicateTermKind.EnumComparison =>
                    _operator == SourcePredicateComparisonOperator.Equal
                        ? rawValue == _enumValue.RawValue
                        : rawValue != _enumValue.RawValue,
                PredicateTermKind.EnumMembership =>
                    (_isNegated ? !ContainsEnumValue(rawValue) : ContainsEnumValue(rawValue)),
                PredicateTermKind.EnumFlags => _flagsMatchMode == SourcePredicateFlagsMatchMode.Any
                    ? (rawValue & _enumValue.RawValue) != 0
                    : (rawValue & _enumValue.RawValue) == _enumValue.RawValue,
                _ => false
            };
        }

        private bool ContainsEnumValue(ulong rawValue)
        {
            var values = _enumValues!;
            if (values.Length <= 8)
            {
                for (var index = 0; index < values.Length; index++)
                {
                    if (values[index].RawValue == rawValue)
                        return true;
                }

                return false;
            }

            var low = 0;
            var high = values.Length - 1;
            while (low <= high)
            {
                var middle = low + ((high - low) >> 1);
                var candidate = values[middle].RawValue;
                if (candidate == rawValue)
                    return true;
                if (candidate < rawValue)
                    low = middle + 1;
                else
                    high = middle - 1;
            }

            return false;
        }

        private static ulong GetEnumRawValue(
            SeparatedValuesParsedValue parsed,
            SeparatedValuesConversion conversion)
        {
            return conversion switch
            {
                SeparatedValuesConversion.Byte => parsed.Byte,
                SeparatedValuesConversion.SByte => unchecked((byte)parsed.SByte),
                SeparatedValuesConversion.Int16 => unchecked((ushort)parsed.Int16),
                SeparatedValuesConversion.UInt16 => parsed.UInt16,
                SeparatedValuesConversion.Int32 => unchecked((uint)parsed.Int32),
                SeparatedValuesConversion.UInt32 => parsed.UInt32,
                SeparatedValuesConversion.Int64 => unchecked((ulong)parsed.Int64),
                SeparatedValuesConversion.UInt64 => parsed.UInt64,
                _ => throw new InvalidOperationException("Enum predicate conversion must be integral.")
            };
        }

        private bool EvaluateLong(SeparatedValuesUtf8Field field, long rowNumber, string path)
        {
            if (!SeparatedValuesValueConverter.TryParse(field, out long value))
                throw InvalidPredicateValue(field, rowNumber, path);
            return Matches(value.CompareTo(_long), _operator);
        }

        private bool EvaluateByte(SeparatedValuesUtf8Field field, long rowNumber, string path)
        {
            if (!SeparatedValuesValueConverter.TryParse(field, out byte value))
                throw InvalidPredicateValue(field, rowNumber, path);
            return Matches(value.CompareTo((byte)_long), _operator);
        }

        private bool EvaluateSByte(SeparatedValuesUtf8Field field, long rowNumber, string path)
        {
            if (!SeparatedValuesValueConverter.TryParse(field, out sbyte value))
                throw InvalidPredicateValue(field, rowNumber, path);
            return Matches(value.CompareTo((sbyte)_long), _operator);
        }

        private bool EvaluateInt16(SeparatedValuesUtf8Field field, long rowNumber, string path)
        {
            if (!SeparatedValuesValueConverter.TryParse(field, out short value))
                throw InvalidPredicateValue(field, rowNumber, path);
            return Matches(value.CompareTo((short)_long), _operator);
        }

        private bool EvaluateInt32(SeparatedValuesUtf8Field field, long rowNumber, string path)
        {
            if (!SeparatedValuesValueConverter.TryParse(field, out int value))
                throw InvalidPredicateValue(field, rowNumber, path);
            return Matches(value.CompareTo((int)_long), _operator);
        }

        private bool EvaluateUInt16(SeparatedValuesUtf8Field field, long rowNumber, string path)
        {
            if (!SeparatedValuesValueConverter.TryParse(field, out ushort value))
                throw InvalidPredicateValue(field, rowNumber, path);
            return Matches(value.CompareTo((ushort)_ulong), _operator);
        }

        private bool EvaluateUInt32(SeparatedValuesUtf8Field field, long rowNumber, string path)
        {
            if (!SeparatedValuesValueConverter.TryParse(field, out uint value))
                throw InvalidPredicateValue(field, rowNumber, path);
            return Matches(value.CompareTo((uint)_ulong), _operator);
        }

        private bool EvaluateULong(SeparatedValuesUtf8Field field, long rowNumber, string path)
        {
            if (!SeparatedValuesValueConverter.TryParse(field, out ulong value))
                throw InvalidPredicateValue(field, rowNumber, path);
            return Matches(value.CompareTo(_ulong), _operator);
        }

        private bool EvaluateDecimal(
            SeparatedValuesUtf8Field field,
            long rowNumber,
            string path,
            IFormatProvider culture)
        {
            if (!SeparatedValuesValueConverter.TryParse(field, out decimal value, culture))
                throw InvalidPredicateValue(field, rowNumber, path);
            return Matches(decimal.Compare(value, _decimal), _operator);
        }

        private bool EvaluateDouble(SeparatedValuesUtf8Field field, long rowNumber, string path)
        {
            if (!SeparatedValuesValueConverter.TryParse(field, out double value))
                throw InvalidPredicateValue(field, rowNumber, path);
            return Matches(value.CompareTo(_double), _operator);
        }

        private bool EvaluateBoolean(SeparatedValuesUtf8Field field, long rowNumber, string path)
        {
            if (!SeparatedValuesValueConverter.TryParse(field, out bool value))
                throw InvalidPredicateValue(field, rowNumber, path);
            return _operator == SourcePredicateComparisonOperator.Equal ? value == _boolean : value != _boolean;
        }

        internal FormatException InvalidPredicateValue(
            SeparatedValuesUtf8Field field,
            long rowNumber,
            string path)
        {
            var observed = field.Decode();
            if (observed.Length > 96)
                observed = observed[..96] + "...";
            if (_enumPlan is not null)
            {
                return new FormatException(
                    $"Separated-values source '{path}' row {rowNumber:N0} column '{_name}' " +
                    $"cannot be converted as enum '{_enumPlan.Descriptor.DisplayName}' " +
                    $"({_enumPlan.BackingKind}); observed '{observed}'.");
            }

            return new FormatException(
                $"Separated-values source '{path}' row {rowNumber:N0} column '{_name}' " +
                $"cannot be converted as {_type}; observed '{observed}'.");
        }

        private static bool Matches(int comparison, SourcePredicateComparisonOperator op)
        {
            return op switch
            {
                SourcePredicateComparisonOperator.Equal => comparison == 0,
                SourcePredicateComparisonOperator.NotEqual => comparison != 0,
                SourcePredicateComparisonOperator.GreaterThan => comparison > 0,
                SourcePredicateComparisonOperator.GreaterOrEqual => comparison >= 0,
                SourcePredicateComparisonOperator.LessThan => comparison < 0,
                SourcePredicateComparisonOperator.LessOrEqual => comparison <= 0,
                _ => false
            };
        }
    }
}

internal readonly struct SeparatedValuesParsedValue
{
    private SeparatedValuesParsedValue(
        SeparatedValuesConversion conversion,
        bool isNull,
        bool boolean = default,
        byte byteValue = default,
        sbyte sbyteValue = default,
        short int16 = default,
        int int32 = default,
        long int64 = default,
        ushort uint16 = default,
        uint uint32 = default,
        ulong uint64 = default,
        decimal decimalValue = default,
        float single = default,
        double doubleValue = default)
    {
        IsAvailable = true;
        IsNull = isNull;
        Conversion = conversion;
        _value = conversion switch
        {
            SeparatedValuesConversion.Boolean => new ParsedValueStorage(boolean),
            SeparatedValuesConversion.Byte => new ParsedValueStorage(byteValue),
            SeparatedValuesConversion.SByte => new ParsedValueStorage(sbyteValue),
            SeparatedValuesConversion.Int16 => new ParsedValueStorage(int16),
            SeparatedValuesConversion.Int32 => new ParsedValueStorage(int32),
            SeparatedValuesConversion.Int64 => new ParsedValueStorage(int64),
            SeparatedValuesConversion.UInt16 => new ParsedValueStorage(uint16),
            SeparatedValuesConversion.UInt32 => new ParsedValueStorage(uint32),
            SeparatedValuesConversion.UInt64 => new ParsedValueStorage(uint64),
            SeparatedValuesConversion.Decimal => new ParsedValueStorage(decimalValue),
            SeparatedValuesConversion.Single => new ParsedValueStorage(single),
            SeparatedValuesConversion.Double => new ParsedValueStorage(doubleValue),
            _ => default
        };
    }

    public bool IsAvailable { get; }

    public bool IsNull { get; }

    public SeparatedValuesConversion Conversion { get; }

    private readonly ParsedValueStorage _value;

    public bool Boolean => _value.Boolean;

    public byte Byte => _value.Byte;

    public sbyte SByte => _value.SByte;

    public short Int16 => _value.Int16;

    public int Int32 => _value.Int32;

    public long Int64 => _value.Int64;

    public ushort UInt16 => _value.UInt16;

    public uint UInt32 => _value.UInt32;

    public ulong UInt64 => _value.UInt64;

    public decimal Decimal => _value.Decimal;

    public float Single => _value.Single;

    public double Double => _value.Double;

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    private readonly struct ParsedValueStorage
    {
        [FieldOffset(0)] public readonly bool Boolean;
        [FieldOffset(0)] public readonly byte Byte;
        [FieldOffset(0)] public readonly sbyte SByte;
        [FieldOffset(0)] public readonly short Int16;
        [FieldOffset(0)] public readonly int Int32;
        [FieldOffset(0)] public readonly long Int64;
        [FieldOffset(0)] public readonly ushort UInt16;
        [FieldOffset(0)] public readonly uint UInt32;
        [FieldOffset(0)] public readonly ulong UInt64;
        [FieldOffset(0)] public readonly decimal Decimal;
        [FieldOffset(0)] public readonly float Single;
        [FieldOffset(0)] public readonly double Double;

        public ParsedValueStorage(bool value) : this() => Boolean = value;
        public ParsedValueStorage(byte value) : this() => Byte = value;
        public ParsedValueStorage(sbyte value) : this() => SByte = value;
        public ParsedValueStorage(short value) : this() => Int16 = value;
        public ParsedValueStorage(int value) : this() => Int32 = value;
        public ParsedValueStorage(long value) : this() => Int64 = value;
        public ParsedValueStorage(ushort value) : this() => UInt16 = value;
        public ParsedValueStorage(uint value) : this() => UInt32 = value;
        public ParsedValueStorage(ulong value) : this() => UInt64 = value;
        public ParsedValueStorage(decimal value) : this() => Decimal = value;
        public ParsedValueStorage(float value) : this() => Single = value;
        public ParsedValueStorage(double value) : this() => Double = value;
    }

    public static SeparatedValuesParsedValue Null(SeparatedValuesConversion conversion)
    {
        return new SeparatedValuesParsedValue(conversion, true);
    }

    public static SeparatedValuesParsedValue FromEnum(
        SeparatedValuesConversion conversion,
        EnumScalarValue value)
    {
        return conversion switch
        {
            SeparatedValuesConversion.Byte => new SeparatedValuesParsedValue(
                conversion, false, byteValue: value.AsByte()),
            SeparatedValuesConversion.SByte => new SeparatedValuesParsedValue(
                conversion, false, sbyteValue: value.AsSByte()),
            SeparatedValuesConversion.Int16 => new SeparatedValuesParsedValue(
                conversion, false, int16: value.AsInt16()),
            SeparatedValuesConversion.UInt16 => new SeparatedValuesParsedValue(
                conversion, false, uint16: value.AsUInt16()),
            SeparatedValuesConversion.Int32 => new SeparatedValuesParsedValue(
                conversion, false, int32: value.AsInt32()),
            SeparatedValuesConversion.UInt32 => new SeparatedValuesParsedValue(
                conversion, false, uint32: value.AsUInt32()),
            SeparatedValuesConversion.Int64 => new SeparatedValuesParsedValue(
                conversion, false, int64: value.AsInt64()),
            SeparatedValuesConversion.UInt64 => new SeparatedValuesParsedValue(
                conversion, false, uint64: value.AsUInt64()),
            _ => throw new ArgumentOutOfRangeException(nameof(conversion), conversion,
                "Enum backing conversion must be integral.")
        };
    }

    public static bool TryParse(
        SeparatedValuesUtf8Field field,
        SeparatedValuesConversion conversion,
        IFormatProvider culture,
        out SeparatedValuesParsedValue parsed)
    {
        switch (conversion)
        {
            case SeparatedValuesConversion.Boolean when SeparatedValuesValueConverter.TryParse(field, out bool value):
                parsed = new SeparatedValuesParsedValue(conversion, false, boolean: value);
                return true;
            case SeparatedValuesConversion.Byte when SeparatedValuesValueConverter.TryParse(field, out byte value):
                parsed = new SeparatedValuesParsedValue(conversion, false, byteValue: value);
                return true;
            case SeparatedValuesConversion.SByte when SeparatedValuesValueConverter.TryParse(field, out sbyte value):
                parsed = new SeparatedValuesParsedValue(conversion, false, sbyteValue: value);
                return true;
            case SeparatedValuesConversion.Int16 when SeparatedValuesValueConverter.TryParse(field, out short value):
                parsed = new SeparatedValuesParsedValue(conversion, false, int16: value);
                return true;
            case SeparatedValuesConversion.Int32 when SeparatedValuesValueConverter.TryParse(field, out int value):
                parsed = new SeparatedValuesParsedValue(conversion, false, int32: value);
                return true;
            case SeparatedValuesConversion.Int64 when SeparatedValuesValueConverter.TryParse(field, out long value):
                parsed = new SeparatedValuesParsedValue(conversion, false, int64: value);
                return true;
            case SeparatedValuesConversion.UInt16 when SeparatedValuesValueConverter.TryParse(field, out ushort value):
                parsed = new SeparatedValuesParsedValue(conversion, false, uint16: value);
                return true;
            case SeparatedValuesConversion.UInt32 when SeparatedValuesValueConverter.TryParse(field, out uint value):
                parsed = new SeparatedValuesParsedValue(conversion, false, uint32: value);
                return true;
            case SeparatedValuesConversion.UInt64 when SeparatedValuesValueConverter.TryParse(field, out ulong value):
                parsed = new SeparatedValuesParsedValue(conversion, false, uint64: value);
                return true;
            case SeparatedValuesConversion.Decimal when SeparatedValuesValueConverter.TryParse(field, out decimal value, culture):
                parsed = new SeparatedValuesParsedValue(conversion, false, decimalValue: value);
                return true;
            case SeparatedValuesConversion.Single when SeparatedValuesValueConverter.TryParse(field, out float value):
                parsed = new SeparatedValuesParsedValue(conversion, false, single: value);
                return true;
            case SeparatedValuesConversion.Double when SeparatedValuesValueConverter.TryParse(field, out double value):
                parsed = new SeparatedValuesParsedValue(conversion, false, doubleValue: value);
                return true;
            default:
                parsed = default;
                return false;
        }
    }

    public bool CanCompare(SeparatedValuesConversion conversion)
    {
        return IsAvailable && !IsNull && Conversion == conversion;
    }

    public bool CanMaterialize(SeparatedValuesConversion conversion)
    {
        return IsAvailable && (IsNull || Conversion == conversion);
    }

    public object? Materialize(SeparatedValuesConversion conversion)
    {
        if (!CanMaterialize(conversion))
            throw new InvalidOperationException("The parsed field cannot satisfy the requested conversion.");
        if (IsNull)
            return null;
        return conversion switch
        {
            SeparatedValuesConversion.Boolean => Boolean,
            SeparatedValuesConversion.Byte => Byte,
            SeparatedValuesConversion.SByte => SByte,
            SeparatedValuesConversion.Int16 => Int16,
            SeparatedValuesConversion.Int32 => Int32,
            SeparatedValuesConversion.Int64 => Int64,
            SeparatedValuesConversion.UInt16 => UInt16,
            SeparatedValuesConversion.UInt32 => UInt32,
            SeparatedValuesConversion.UInt64 => UInt64,
            SeparatedValuesConversion.Decimal => Decimal,
            SeparatedValuesConversion.Single => Single,
            SeparatedValuesConversion.Double => Double,
            _ => throw new InvalidOperationException("The parsed conversion is not materializable.")
        };
    }
}

internal enum SeparatedValuesConversion : byte
{
    String,
    Boolean,
    Byte,
    Character,
    DateTime,
    DateTimeOffset,
    Decimal,
    Double,
    Int16,
    Int32,
    Int64,
    SByte,
    Single,
    TimeSpan,
    UInt16,
    UInt32,
    UInt64,
    Guid,
    DateOnly,
    TimeOnly
}

internal static class SeparatedValuesValueConverter
{
    public static bool TryGetExactConversion(Type fieldType, out SeparatedValuesConversion conversion)
    {
        ArgumentNullException.ThrowIfNull(fieldType);
        var type = Nullable.GetUnderlyingType(fieldType) ?? fieldType;

        if (type == typeof(string)) conversion = SeparatedValuesConversion.String;
        else if (type == typeof(bool)) conversion = SeparatedValuesConversion.Boolean;
        else if (type == typeof(byte)) conversion = SeparatedValuesConversion.Byte;
        else if (type == typeof(char)) conversion = SeparatedValuesConversion.Character;
        else if (type == typeof(DateTime)) conversion = SeparatedValuesConversion.DateTime;
        else if (type == typeof(DateTimeOffset)) conversion = SeparatedValuesConversion.DateTimeOffset;
        else if (type == typeof(decimal)) conversion = SeparatedValuesConversion.Decimal;
        else if (type == typeof(double)) conversion = SeparatedValuesConversion.Double;
        else if (type == typeof(short)) conversion = SeparatedValuesConversion.Int16;
        else if (type == typeof(int)) conversion = SeparatedValuesConversion.Int32;
        else if (type == typeof(long)) conversion = SeparatedValuesConversion.Int64;
        else if (type == typeof(sbyte)) conversion = SeparatedValuesConversion.SByte;
        else if (type == typeof(float)) conversion = SeparatedValuesConversion.Single;
        else if (type == typeof(TimeSpan)) conversion = SeparatedValuesConversion.TimeSpan;
        else if (type == typeof(ushort)) conversion = SeparatedValuesConversion.UInt16;
        else if (type == typeof(uint)) conversion = SeparatedValuesConversion.UInt32;
        else if (type == typeof(ulong)) conversion = SeparatedValuesConversion.UInt64;
        else if (type == typeof(Guid)) conversion = SeparatedValuesConversion.Guid;
        else if (type == typeof(DateOnly)) conversion = SeparatedValuesConversion.DateOnly;
        else if (type == typeof(TimeOnly)) conversion = SeparatedValuesConversion.TimeOnly;
        else
        {
            conversion = default;
            return false;
        }

        return fieldType == type || Nullable.GetUnderlyingType(fieldType) == type;
    }

    public static SeparatedValuesConversion GetConversion(Type outputType, StructuredTypeState inferredType)
    {
        var type = outputType.GetUnderlyingNullable();
        if (type == typeof(object))
        {
            type = inferredType.Kind switch
            {
                StructuredValueKind.Boolean => typeof(bool),
                StructuredValueKind.Long => typeof(long),
                StructuredValueKind.Decimal => typeof(decimal),
                StructuredValueKind.Double => typeof(double),
                _ => typeof(string)
            };
        }

        if (TryGetExactConversion(type, out var conversion))
            return conversion;

        throw new NotSupportedException($"Explicit separated-values type '{outputType}' is not supported.");
    }

    public static object? Convert(
        SeparatedValuesUtf8Field field,
        SeparatedValuesConversion conversion,
        string columnName,
        long rowNumber,
        IFormatProvider? provider = null)
    {
        if (IsNull(field))
            return null;

        try
        {
            return conversion switch
            {
                SeparatedValuesConversion.String => field.Decode(),
                SeparatedValuesConversion.Boolean when TryParse(field, out bool value) => value,
                SeparatedValuesConversion.Byte when TryParse(field, out byte value) => value,
                SeparatedValuesConversion.Character => ParseCharacter(field),
                SeparatedValuesConversion.DateTime => ParseDateTime(field, provider),
                SeparatedValuesConversion.DateTimeOffset => ParseDateTimeOffset(field, provider),
                SeparatedValuesConversion.Decimal when TryParse(field, out decimal value, provider) => value,
                SeparatedValuesConversion.Double when TryParse(field, out double value) => value,
                SeparatedValuesConversion.Int16 when TryParse(field, out short value) => value,
                SeparatedValuesConversion.Int32 when TryParse(field, out int value) => value,
                SeparatedValuesConversion.Int64 when TryParse(field, out long value) => value,
                SeparatedValuesConversion.SByte when TryParse(field, out sbyte value) => value,
                SeparatedValuesConversion.Single when TryParse(field, out float value) => value,
                SeparatedValuesConversion.TimeSpan => ParseTimeSpan(field, provider),
                SeparatedValuesConversion.UInt16 when TryParse(field, out ushort value) => value,
                SeparatedValuesConversion.UInt32 when TryParse(field, out uint value) => value,
                SeparatedValuesConversion.UInt64 when TryParse(field, out ulong value) => value,
                SeparatedValuesConversion.Guid => ParseGuid(field),
                SeparatedValuesConversion.DateOnly => ParseDateOnly(field, provider),
                SeparatedValuesConversion.TimeOnly => ParseTimeOnly(field, provider),
                _ => throw new FormatException()
            };
        }
        catch (Exception exception) when (exception is FormatException or OverflowException)
        {
            throw new FormatException(
                $"Separated-values row {rowNumber:N0} column '{columnName}' cannot be converted as {conversion}.",
                exception);
        }
    }

    public static bool IsNull(SeparatedValuesUtf8Field field)
    {
        return !field.WasQuoted && (field.EncodedValue.IsEmpty || field.IsNullToken);
    }

    public static IFormatProvider GetCulture(string cultureName)
    {
        return string.Equals(cultureName, "invariant", StringComparison.OrdinalIgnoreCase)
            ? CultureInfo.InvariantCulture
            : CultureInfo.GetCultureInfo(cultureName);
    }

    public static bool TryParse(SeparatedValuesUtf8Field field, out bool value)
    {
        var bytes = field.EncodedValue;
        if (field.NeedsUnescaping)
        {
            value = false;
            return false;
        }

        if (bytes.Length == 4 &&
            ToLowerAscii(bytes[0]) == (byte)'t' &&
            ToLowerAscii(bytes[1]) == (byte)'r' &&
            ToLowerAscii(bytes[2]) == (byte)'u' &&
            ToLowerAscii(bytes[3]) == (byte)'e')
        {
            value = true;
            return true;
        }

        if (bytes.Length == 5 &&
            ToLowerAscii(bytes[0]) == (byte)'f' &&
            ToLowerAscii(bytes[1]) == (byte)'a' &&
            ToLowerAscii(bytes[2]) == (byte)'l' &&
            ToLowerAscii(bytes[3]) == (byte)'s' &&
            ToLowerAscii(bytes[4]) == (byte)'e')
        {
            value = false;
            return true;
        }

        value = false;
        return false;
    }

    public static bool TryParse(SeparatedValuesUtf8Field field, out byte value)
    {
        if (!field.NeedsUnescaping &&
            Utf8Parser.TryParse(field.EncodedValue, out value, out var consumed) &&
            consumed == field.EncodedValue.Length)
            return true;
        value = default;
        return false;
    }

    public static bool TryParse(SeparatedValuesUtf8Field field, out sbyte value)
    {
        if (!field.NeedsUnescaping &&
            Utf8Parser.TryParse(field.EncodedValue, out value, out var consumed) &&
            consumed == field.EncodedValue.Length)
            return true;
        value = default;
        return false;
    }

    public static bool TryParse(SeparatedValuesUtf8Field field, out short value)
    {
        if (!field.NeedsUnescaping &&
            Utf8Parser.TryParse(field.EncodedValue, out value, out var consumed) &&
            consumed == field.EncodedValue.Length)
            return true;
        value = default;
        return false;
    }

    public static bool TryParse(SeparatedValuesUtf8Field field, out ushort value)
    {
        if (!field.NeedsUnescaping &&
            Utf8Parser.TryParse(field.EncodedValue, out value, out var consumed) &&
            consumed == field.EncodedValue.Length)
            return true;
        value = default;
        return false;
    }

    public static bool TryParse(SeparatedValuesUtf8Field field, out int value)
    {
        if (!field.NeedsUnescaping &&
            Utf8Parser.TryParse(field.EncodedValue, out value, out var consumed) &&
            consumed == field.EncodedValue.Length)
            return true;
        value = default;
        return false;
    }

    public static bool TryParse(SeparatedValuesUtf8Field field, out uint value)
    {
        if (!field.NeedsUnescaping &&
            Utf8Parser.TryParse(field.EncodedValue, out value, out var consumed) &&
            consumed == field.EncodedValue.Length)
            return true;
        value = default;
        return false;
    }

    public static bool TryParse(SeparatedValuesUtf8Field field, out long value)
    {
        if (!field.NeedsUnescaping &&
            Utf8Parser.TryParse(field.EncodedValue, out value, out var consumed) &&
            consumed == field.EncodedValue.Length)
            return true;
        value = default;
        return false;
    }

    public static bool TryParse(SeparatedValuesUtf8Field field, out ulong value)
    {
        if (!field.NeedsUnescaping &&
            Utf8Parser.TryParse(field.EncodedValue, out value, out var consumed) &&
            consumed == field.EncodedValue.Length)
            return true;
        value = default;
        return false;
    }

    public static bool TryParse(SeparatedValuesUtf8Field field, out decimal value)
    {
        return TryParse(field, out value, CultureInfo.InvariantCulture);
    }

    public static bool TryParse(
        SeparatedValuesUtf8Field field,
        out decimal value,
        IFormatProvider? provider)
    {
        if (field.NeedsUnescaping)
        {
            value = default;
            return false;
        }

        if (provider is CultureInfo culture && !ReferenceEquals(culture, CultureInfo.InvariantCulture))
        {
            return decimal.TryParse(
                field.Decode(),
                NumberStyles.Number,
                culture,
                out value);
        }

        return SeparatedValuesDecimalParser.TryParse(field.EncodedValue, out value);
    }

    public static bool TryParse(SeparatedValuesUtf8Field field, out float value)
    {
        if (!field.NeedsUnescaping &&
            Utf8Parser.TryParse(field.EncodedValue, out value, out var consumed) &&
            consumed == field.EncodedValue.Length &&
            float.IsFinite(value))
            return true;
        value = default;
        return false;
    }

    public static bool TryParse(SeparatedValuesUtf8Field field, out double value)
    {
        if (!field.NeedsUnescaping &&
            Utf8Parser.TryParse(field.EncodedValue, out value, out var consumed) &&
            consumed == field.EncodedValue.Length &&
            double.IsFinite(value))
            return true;
        value = default;
        return false;
    }

    private static char ParseCharacter(SeparatedValuesUtf8Field field)
    {
        var value = field.Decode();
        return value.Length == 1 ? value[0] : throw new FormatException();
    }

    private static DateTime ParseDateTime(SeparatedValuesUtf8Field field, IFormatProvider? provider)
    {
        return DateTime.TryParse(field.Decode(), provider ?? CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
            ? value
            : throw new FormatException();
    }

    private static DateTimeOffset ParseDateTimeOffset(SeparatedValuesUtf8Field field, IFormatProvider? provider)
    {
        return DateTimeOffset.TryParse(field.Decode(), provider ?? CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
            ? value
            : throw new FormatException();
    }

    private static TimeSpan ParseTimeSpan(SeparatedValuesUtf8Field field, IFormatProvider? provider)
    {
        return TimeSpan.TryParse(field.Decode(), provider ?? CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new FormatException();
    }

    private static Guid ParseGuid(SeparatedValuesUtf8Field field)
    {
        return Guid.TryParse(field.Decode(), out var value) ? value : throw new FormatException();
    }

    private static DateOnly ParseDateOnly(SeparatedValuesUtf8Field field, IFormatProvider? provider)
    {
        return DateOnly.TryParse(field.Decode(), provider ?? CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
            ? value
            : throw new FormatException();
    }

    private static TimeOnly ParseTimeOnly(SeparatedValuesUtf8Field field, IFormatProvider? provider)
    {
        return TimeOnly.TryParse(field.Decode(), provider ?? CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
            ? value
            : throw new FormatException();
    }

    private static byte ToLowerAscii(byte value)
    {
        return value is >= (byte)'A' and <= (byte)'Z'
            ? (byte)(value + ((byte)'a' - (byte)'A'))
            : value;
    }

}
