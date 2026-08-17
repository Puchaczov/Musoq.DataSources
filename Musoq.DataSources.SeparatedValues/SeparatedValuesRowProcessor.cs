#nullable enable

using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Musoq.DataSources.Common;
using Musoq.DataSources.Structured;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Helpers;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues;

internal sealed class SeparatedValuesRowProcessor
{
    private readonly CancellationToken _cancellationToken;
    private readonly int _chunkSize;
    private readonly SeparatedValuesRecordKernel _kernel;
    private readonly DataSourceProgressReporter? _progress;
    private readonly long _rowNumberOffset;
    private readonly long? _take;
    private readonly IChunkWriter<object?[]> _writer;
    private List<object?[]>? _chunk;
    private long _emittedRows;
    private long _skipRemaining;
    private int _zeroColumnRows;

    public SeparatedValuesRowProcessor(
        SeparatedValuesSourceContract contract,
        SourceExecutionContext executionContext,
        IChunkWriter<object?[]> writer,
        DataSourceProgressReporter? progress,
        int chunkSize,
        CancellationToken cancellationToken,
        long rowNumberOffset = 0,
        long? skipOverride = null)
    {
        ArgumentNullException.ThrowIfNull(contract);
        var snapshot = contract.Snapshot;
        _writer = writer;
        _progress = progress;
        _chunkSize = chunkSize;
        _cancellationToken = cancellationToken;
        _rowNumberOffset = rowNumberOffset;
        _skipRemaining = skipOverride ?? executionContext.Plan.AcceptedSkip ?? 0;
        _take = executionContext.Plan.AcceptedTake;
        _kernel = SeparatedValuesRecordKernel.Create(contract, executionContext);
    }

    public long RowsRead { get; private set; }

    public long RowsEmitted => _emittedRows;

    public bool HasWork => _take is null || _take.Value > 0;

    public bool Process(SeparatedValuesUtf8Record record)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        RowsRead++;
        _progress?.RowRead();
        var rowNumber = _rowNumberOffset + RowsRead;

        if (!_kernel.Prepare(record, rowNumber))
            return true;

        if (_skipRemaining > 0)
        {
            _skipRemaining--;
            return true;
        }

        if (_take is not null && _emittedRows >= _take.Value)
            return false;

        if (_kernel.HasOutputColumns)
        {
            _chunk ??= new List<object?[]>(_chunkSize);
            _chunk.Add(_kernel.Materialize(record, rowNumber));
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
            _writer.Write(new RepeatedValueChunk<object?[]>(Array.Empty<object?>(), _zeroColumnRows));
            _zeroColumnRows = 0;
        }

        if (_chunk is null || _chunk.Count == 0)
            return;

        _writer.Write(_chunk);
        _chunk = null;
    }
}

internal sealed class SeparatedValuesRecordKernel
{
    private readonly SeparatedValuesRowLayout _layout;
    private readonly SeparatedValuesPredicateEvaluator _predicate;
    private readonly SeparatedValuesSchemaValidator _schemaValidator;

    private SeparatedValuesRecordKernel(
        SeparatedValuesRowLayout layout,
        SeparatedValuesPredicateEvaluator predicate,
        SeparatedValuesSchemaValidator schemaValidator)
    {
        _layout = layout;
        _predicate = predicate;
        _schemaValidator = schemaValidator;
    }

    public bool HasOutputColumns => _layout.HasOutputColumns;

    public long MaterializedRowCount { get; private set; }

    public long FieldsVisited { get; private set; }

    public static SeparatedValuesRecordKernel Create(
        SeparatedValuesSourceContract contract,
        SourceExecutionContext executionContext)
    {
        return new SeparatedValuesRecordKernel(
            SeparatedValuesRowLayout.Create(contract, executionContext),
            SeparatedValuesPredicateEvaluator.Create(
                contract,
                executionContext.Plan.AcceptedPredicate),
            SeparatedValuesSchemaValidator.Create(contract));
    }

    public bool Prepare(SeparatedValuesUtf8Record record, long rowNumber)
    {
        _layout.BeginRecord();
        var predicateTermIndex = 0;
        var predicateMatched = true;
        var bindingIndex = 0;
        var fieldIndex = 0;

        foreach (var field in record)
        {
            FieldsVisited++;
            _schemaValidator.ValidateField(fieldIndex, field, rowNumber);
            if (predicateMatched)
            {
                predicateMatched = _predicate.EvaluateField(
                    fieldIndex,
                    field,
                    rowNumber,
                    ref predicateTermIndex);
            }

            if (predicateMatched || !_predicate.HasTerms)
                _layout.CaptureField(fieldIndex, field, ref bindingIndex);
            fieldIndex++;
        }

        return predicateMatched && _predicate.IsComplete(predicateTermIndex);
    }

    public object?[] Materialize(SeparatedValuesUtf8Record record, long rowNumber)
    {
        MaterializedRowCount++;
        return _layout.Materialize(record, rowNumber);
    }
}

internal sealed class SeparatedValuesSchemaValidator
{
    private static readonly SeparatedValuesSchemaValidator Empty = new(null, [], string.Empty);
    private readonly StructuredColumnSnapshot[] _columns;
    private readonly string _path;

    private SeparatedValuesSchemaValidator(
        SeparatedValuesSourceContract? contract,
        StructuredColumnSnapshot[] columns,
        string path)
    {
        Contract = contract;
        _columns = columns;
        _path = path;
    }

    private SeparatedValuesSourceContract? Contract { get; }

    public static SeparatedValuesSchemaValidator Create(SeparatedValuesSourceContract contract)
    {
        return contract.Mode == SeparatedValuesSchemaResolutionMode.Sampled
            ? new SeparatedValuesSchemaValidator(
                contract,
                contract.Snapshot.Columns.ToArray(),
                contract.Snapshot.Identity.CanonicalPath)
            : contract.Snapshot.Columns.Length == 0
                ? Empty
                : new SeparatedValuesSchemaValidator(contract, [], contract.Snapshot.Identity.CanonicalPath);
    }

    public void ValidateField(int fieldIndex, SeparatedValuesUtf8Field field, long rowNumber)
    {
        if (Contract is null)
            return;

        var expectedWidth = Contract.Snapshot.Columns.Length;
        if (fieldIndex >= expectedWidth)
        {
            throw new StructuredSchemaDriftException(
                _path,
                $"row {rowNumber:N0} contains more than the bound {expectedWidth:N0} columns");
        }

        if (_columns.Length > 0)
            ValidateSampledType(_columns[fieldIndex], field, rowNumber);
    }

    private void ValidateSampledType(
        StructuredColumnSnapshot column,
        SeparatedValuesUtf8Field field,
        long rowNumber)
    {
        if (SeparatedValuesValueConverter.IsNull(field))
            return;

        var valid = column.TypeState.Kind switch
        {
            StructuredValueKind.Boolean => SeparatedValuesValueConverter.TryParse(field, out bool _),
            StructuredValueKind.Long => SeparatedValuesValueConverter.TryParse(field, out long _),
            StructuredValueKind.Decimal => SeparatedValuesValueConverter.TryParse(field, out decimal _),
            StructuredValueKind.Double => SeparatedValuesValueConverter.TryParse(field, out double _),
            StructuredValueKind.String => true,
            _ => true
        };
        if (valid)
            return;

        var observed = field.Decode();
        if (observed.Length > 96)
            observed = observed[..96] + "...";
        throw new FormatException(
            $"Separated-values source '{_path}' row {rowNumber:N0} column '{column.Name}' " +
            $"expected {column.TypeState.Kind} but observed '{observed}'.");
    }
}

internal sealed class SeparatedValuesRowLayout
{
    private readonly BoundColumn[] _columns;
    private readonly FieldLocation[] _locations;
    private readonly int _outputCount;
    private readonly StructuredStringPool _stringPool;
    private readonly IFormatProvider _culture;

    private SeparatedValuesRowLayout(
        BoundColumn[] columns,
        int outputCount,
        StructuredStringPool stringPool,
        IFormatProvider culture)
    {
        _columns = columns;
        _locations = new FieldLocation[columns.Length];
        _outputCount = outputCount;
        _stringPool = stringPool;
        _culture = culture;
    }

    public static SeparatedValuesRowLayout Create(
        SeparatedValuesSourceContract contract,
        SourceExecutionContext executionContext)
    {
        var snapshot = contract.Snapshot;
        var columns = new List<BoundColumn>();
        var outputCount = 0;
        var readPlan = SeparatedValuesReadPlan.From(executionContext.Plan);
        var projectionAccepted = readPlan.ProjectionAccepted || executionContext.Plan.AcceptedColumns.Count > 0;

        if (projectionAccepted)
        {
            var denseFallback = 0;
            foreach (var accepted in executionContext.Plan.AcceptedColumns)
            {
                var name = ResolveName(snapshot, accepted.Name);
                var schemaColumn = FindSchemaColumn(executionContext.AllColumns, name);
                var outputIndex = schemaColumn?.ColumnIndex ?? denseFallback;
                var outputType = schemaColumn?.ColumnType ?? GetSnapshotColumn(snapshot, name).ClrType;
                AddColumn(columns, snapshot, name, outputIndex, outputType);
                outputCount = Math.Max(outputCount, outputIndex + 1);
                denseFallback++;
            }
        }
        else if (executionContext.AllColumns.Count > 0)
        {
            foreach (var schemaColumn in executionContext.AllColumns.OrderBy(column => column.ColumnIndex))
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

        return new SeparatedValuesRowLayout(
            columns.OrderBy(column => column.SourceOrdinal).ToArray(),
            outputCount,
            snapshot.StringPool,
            SeparatedValuesValueConverter.GetCulture(contract.Dialect.CultureName));
    }

    public bool HasOutputColumns => _outputCount > 0;

    public void BeginRecord()
    {
        Array.Clear(_locations);
    }

    public void CaptureField(
        int fieldIndex,
        SeparatedValuesUtf8Field field,
        ref int bindingIndex)
    {
        while (bindingIndex < _columns.Length && _columns[bindingIndex].SourceOrdinal < fieldIndex)
            bindingIndex++;

        while (bindingIndex < _columns.Length && _columns[bindingIndex].SourceOrdinal == fieldIndex)
        {
            _locations[bindingIndex] = new FieldLocation(
                field.EncodedOffset,
                field.EncodedValue.Length,
                field.WasQuoted,
                field.NeedsUnescaping,
                field.EscapeMode,
                field.IsNullToken,
                field.Quote,
                true);
            bindingIndex++;
        }
    }

    public object?[] Materialize(SeparatedValuesUtf8Record record, long rowNumber)
    {
        if (_outputCount == 0)
            return Array.Empty<object?>();

        var output = new object?[_outputCount];
        for (var bindingIndex = 0; bindingIndex < _columns.Length; bindingIndex++)
        {
            ref readonly var binding = ref _columns[bindingIndex];
            ref readonly var location = ref _locations[bindingIndex];
            if (!location.Present)
                continue;

            var field = new SeparatedValuesUtf8Field(
                record.Bytes.Slice(location.Offset, location.Length),
                location.Offset,
                location.WasQuoted,
                location.NeedsUnescaping,
                location.EscapeMode,
                location.IsNullToken,
                location.Quote);
            output[binding.OutputOrdinal] = binding.Conversion == SeparatedValuesConversion.String &&
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

    private static void AddColumn(
        List<BoundColumn> columns,
        StructuredSchemaSnapshot snapshot,
        string name,
        int outputOrdinal,
        Type outputType)
    {
        var snapshotColumn = GetSnapshotColumn(snapshot, name);
        columns.Add(new BoundColumn(
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
        if (dotIndex >= 0)
        {
            var unqualified = name[(dotIndex + 1)..];
            if (snapshot.TryGetColumn(unqualified, out _))
                return unqualified;
        }

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

    private readonly record struct BoundColumn(
        string Name,
        int SourceOrdinal,
        int OutputOrdinal,
        SeparatedValuesConversion Conversion);

    private readonly record struct FieldLocation(
        int Offset,
        int Length,
        bool WasQuoted,
        bool NeedsUnescaping,
        SeparatedValuesEscapeMode EscapeMode,
        bool IsNullToken,
        byte? Quote,
        bool Present);
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

    public bool EvaluateField(
        int fieldIndex,
        SeparatedValuesUtf8Field field,
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
            if (!_terms[termIndex].Evaluate(field, rowNumber, _path, _culture))
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
                SeparatedValuesSourcePlanner.TryGetComparisonParts(
                    comparison,
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
        private readonly bool _boolean;
        private readonly decimal _decimal;
        private readonly double _double;
        private readonly long _long;
        private readonly ulong _ulong;
        private readonly string _name;
        private readonly SourcePredicateComparisonOperator _operator;
        private readonly byte[]? _stringUtf8;
        private readonly StructuredValueKind _type;
        private readonly SeparatedValuesConversion _conversion;

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
            SeparatedValuesConversion conversion)
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
        }

        public int SourceOrdinal { get; }

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

        public bool Evaluate(
            SeparatedValuesUtf8Field field,
            long rowNumber,
            string path,
            IFormatProvider culture)
        {
            if (SeparatedValuesValueConverter.IsNull(field))
                return false;

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

        private FormatException InvalidPredicateValue(
            SeparatedValuesUtf8Field field,
            long rowNumber,
            string path)
        {
            var observed = field.Decode();
            if (observed.Length > 96)
                observed = observed[..96] + "...";
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

        if (type == typeof(string)) return SeparatedValuesConversion.String;
        if (type == typeof(bool)) return SeparatedValuesConversion.Boolean;
        if (type == typeof(byte)) return SeparatedValuesConversion.Byte;
        if (type == typeof(char)) return SeparatedValuesConversion.Character;
        if (type == typeof(DateTime)) return SeparatedValuesConversion.DateTime;
        if (type == typeof(DateTimeOffset)) return SeparatedValuesConversion.DateTimeOffset;
        if (type == typeof(decimal)) return SeparatedValuesConversion.Decimal;
        if (type == typeof(double)) return SeparatedValuesConversion.Double;
        if (type == typeof(short)) return SeparatedValuesConversion.Int16;
        if (type == typeof(int)) return SeparatedValuesConversion.Int32;
        if (type == typeof(long)) return SeparatedValuesConversion.Int64;
        if (type == typeof(sbyte)) return SeparatedValuesConversion.SByte;
        if (type == typeof(float)) return SeparatedValuesConversion.Single;
        if (type == typeof(TimeSpan)) return SeparatedValuesConversion.TimeSpan;
        if (type == typeof(ushort)) return SeparatedValuesConversion.UInt16;
        if (type == typeof(uint)) return SeparatedValuesConversion.UInt32;
        if (type == typeof(ulong)) return SeparatedValuesConversion.UInt64;
        if (type == typeof(Guid)) return SeparatedValuesConversion.Guid;
        if (type == typeof(DateOnly)) return SeparatedValuesConversion.DateOnly;
        if (type == typeof(TimeOnly)) return SeparatedValuesConversion.TimeOnly;

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

    public static bool TryParse(SeparatedValuesUtf8Field field, out byte value) =>
        TryParseCore(field, Utf8Parser.TryParse, out value);

    public static bool TryParse(SeparatedValuesUtf8Field field, out sbyte value) =>
        TryParseCore(field, Utf8Parser.TryParse, out value);

    public static bool TryParse(SeparatedValuesUtf8Field field, out short value) =>
        TryParseCore(field, Utf8Parser.TryParse, out value);

    public static bool TryParse(SeparatedValuesUtf8Field field, out ushort value) =>
        TryParseCore(field, Utf8Parser.TryParse, out value);

    public static bool TryParse(SeparatedValuesUtf8Field field, out int value) =>
        TryParseCore(field, Utf8Parser.TryParse, out value);

    public static bool TryParse(SeparatedValuesUtf8Field field, out uint value) =>
        TryParseCore(field, Utf8Parser.TryParse, out value);

    public static bool TryParse(SeparatedValuesUtf8Field field, out long value) =>
        TryParseCore(field, Utf8Parser.TryParse, out value);

    public static bool TryParse(SeparatedValuesUtf8Field field, out ulong value) =>
        TryParseCore(field, Utf8Parser.TryParse, out value);

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
        var result = TryParseCore(field, Utf8Parser.TryParse, out value);
        return result && float.IsFinite(value);
    }

    public static bool TryParse(SeparatedValuesUtf8Field field, out double value)
    {
        var result = TryParseCore(field, Utf8Parser.TryParse, out value);
        return result && double.IsFinite(value);
    }

    private static bool TryParseCore<T>(
        SeparatedValuesUtf8Field field,
        Utf8TryParse<T> parser,
        out T value)
    {
        if (field.NeedsUnescaping ||
            !parser(field.EncodedValue, out value, out var consumed) ||
            consumed != field.EncodedValue.Length)
        {
            value = default!;
            return false;
        }

        return true;
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

    private delegate bool Utf8TryParse<T>(ReadOnlySpan<byte> source, out T value, out int bytesConsumed, char format = default);
}
