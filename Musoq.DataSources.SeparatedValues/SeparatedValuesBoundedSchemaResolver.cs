#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Musoq.DataSources.Structured;
using Musoq.Schema;

namespace Musoq.DataSources.SeparatedValues;

internal readonly record struct SeparatedValuesInferenceOptions(
    long MaximumBytes,
    int MaximumRows,
    TimeSpan MaximumTime)
{
    public const string MaximumBytesSettingName = "separatedvalues.inference_max_bytes";
    public const string MaximumRowsSettingName = "separatedvalues.inference_max_rows";
    public const string MaximumTimeMillisecondsSettingName = "separatedvalues.inference_max_time_ms";
    public const long DefaultMaximumBytes = 1024 * 1024;
    public const int DefaultMaximumRows = 4096;
    public const int DefaultMaximumTimeMilliseconds = 10;

    public static SeparatedValuesInferenceOptions From(IReadOnlyDictionary<string, string> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new SeparatedValuesInferenceOptions(
            ParseLong(settings, MaximumBytesSettingName, DefaultMaximumBytes),
            checked((int)ParseLong(settings, MaximumRowsSettingName, DefaultMaximumRows, int.MaxValue)),
            TimeSpan.FromMilliseconds(ParseLong(
                settings,
                MaximumTimeMillisecondsSettingName,
                DefaultMaximumTimeMilliseconds,
                int.MaxValue)));
    }

    private static long ParseLong(
        IReadOnlyDictionary<string, string> settings,
        string name,
        long defaultValue,
        long maximum = long.MaxValue)
    {
        if (!settings.TryGetValue(name, out var text) || string.IsNullOrWhiteSpace(text))
            return defaultValue;

        if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ||
            value <= 0 ||
            value > maximum)
        {
            throw new ArgumentException(
                $"Runtime setting '{name}' must be an integer between 1 and {maximum:N0}.",
                nameof(settings));
        }

        return value;
    }
}

internal sealed class BoundedSeparatedValuesSchemaResolver : ISeparatedValuesSchemaResolver
{
    private const int FingerprintEdgeBytes = 64 * 1024;
    private const int InferenceBufferSize = 64 * 1024;

    public SeparatedValuesSourceContract Resolve(SeparatedValuesSchemaResolutionRequest request)
    {
        var started = Stopwatch.GetTimestamp();
        var options = SeparatedValuesInferenceOptions.From(request.RuntimeSettings);
        var separator = SeparatedValuesFormat.GetSeparatorByte(request.Separator);
        var dialect = request.Dialect ?? SeparatedValuesDialect.Strict(separator);
        var parserOptions = SeparatedValuesFormat.CreateParserOptions(
            dialect,
            request.HasHeader,
            request.SkipLines);
        var identity = StructuredFileIdentity.Capture(
            request.Path,
            parserOptions,
            request.CancellationToken);
        var identityBytes = EstimateFingerprintBytes(identity.Length);
        var remainingBytes = options.MaximumBytes - identityBytes;
        var acquisitionStarted = Stopwatch.GetTimestamp();
        var deadline = CreateDeadline(acquisitionStarted, options.MaximumTime);

        if (remainingBytes <= 0 || Stopwatch.GetTimestamp() >= deadline)
            throw BudgetExceeded(identity.CanonicalPath, options, "before the header or first record could be read");

        using var reader = new SeparatedValuesUtf8Reader(
            identity.CanonicalPath,
            dialect,
            request.SkipLines,
            (int)Math.Min(InferenceBufferSize, remainingBytes),
            remainingBytes,
            deadline,
            request.CancellationToken);
        reader.Prepare();
        if (reader.BudgetExhausted && reader.SkippedLineCount < request.SkipLines)
            throw BudgetExceeded(identity.CanonicalPath, options, "while skipping physical preamble lines");

        var declaredColumns = GetConcreteDeclaredColumns(request.DeclaredColumns);
        var contract = declaredColumns is not null
            ? ResolveDeclared(request, identity, reader, declaredColumns, identityBytes, started, options, dialect)
            : ResolveSampled(request, identity, reader, identityBytes, started, acquisitionStarted, options, dialect);
        return SeparatedValuesStructuralSummaryCache.TryGet(identity, out var summary) &&
               summary.DataStartOffset == contract.DataStartOffset
            ? contract.WithStructuralSummary(summary)
            : contract;
    }

    private static SeparatedValuesSourceContract ResolveDeclared(
        SeparatedValuesSchemaResolutionRequest request,
        StructuredFileIdentity identity,
        SeparatedValuesUtf8Reader reader,
        ISchemaColumn[] declaredColumns,
        long identityBytes,
        long started,
        SeparatedValuesInferenceOptions options,
        SeparatedValuesDialect dialect)
    {
        string[] names;
        Type[] types;
        long dataStartOffset;
        var inspectedRows = 0L;

        if (request.HasHeader)
        {
            if (!reader.TryRead(out var header))
            {
                if (reader.BudgetExhausted)
                    throw BudgetExceeded(identity.CanonicalPath, options, "before the complete header was read");
                throw new InvalidDataException("A headered separated-values source must contain a non-empty header record.");
            }

            names = ReadHeaderNames(header);
            dataStartOffset = header.EndOffset;
            types = BindDeclaredColumns(identity.CanonicalPath, names, declaredColumns, request.HasHeader);
        }
        else
        {
            dataStartOffset = reader.NextRecordOffset;
            var orderedDeclared = declaredColumns.OrderBy(column => column.ColumnIndex).ToArray();
            var positionalContract = orderedDeclared.Length > 0 &&
                                      orderedDeclared.All(column => TryGetAutoColumnOrdinal(column.ColumnName, out _));
            var namedContract = orderedDeclared.Length > 0 && !positionalContract;
            names = namedContract
                ? orderedDeclared.Select(column => column.ColumnName).ToArray()
                : [];
            if (reader.TryRead(out var firstRecord))
            {
                inspectedRows = 1;
                var width = CountFields(firstRecord);
                if (namedContract && width != orderedDeclared.Length)
                {
                    throw new StructuredSchemaDriftException(
                        identity.CanonicalPath,
                        $"the first headerless record contains {width:N0} columns but the TABLE contract declares " +
                        $"{orderedDeclared.Length:N0}");
                }

                if (!namedContract)
                {
                    names = Enumerable.Range(1, width)
                        .Select(index => string.Format(
                            CultureInfo.InvariantCulture,
                            SeparatedValuesHelper.AutoColumnName,
                            index))
                        .ToArray();
                }
            }
            else if (reader.BudgetExhausted)
            {
                throw BudgetExceeded(identity.CanonicalPath, options, "before the complete first record was read");
            }
            else
            {
                names = declaredColumns
                    .OrderBy(column => column.ColumnIndex)
                    .Select(column => column.ColumnName)
                    .ToArray();
            }

            types = BindDeclaredColumns(identity.CanonicalPath, names, declaredColumns, request.HasHeader);
        }

        var columns = new StructuredColumnSnapshot[names.Length];
        for (var index = 0; index < names.Length; index++)
        {
            columns[index] = new StructuredColumnSnapshot(
                names[index],
                index,
                ToStructuredType(types[index]),
                0);
        }

        var snapshot = new StructuredSchemaSnapshot(identity, columns, 0);
        return new SeparatedValuesSourceContract(
            snapshot,
            SeparatedValuesSchemaResolutionMode.Declared,
            false,
            inspectedRows,
            checked(identityBytes + reader.BytesRead),
            Stopwatch.GetElapsedTime(started),
            dataStartOffset,
            types,
            null,
            dialect);
    }

    private static SeparatedValuesSourceContract ResolveSampled(
        SeparatedValuesSchemaResolutionRequest request,
        StructuredFileIdentity identity,
        SeparatedValuesUtf8Reader reader,
        long identityBytes,
        long started,
        long acquisitionStarted,
        SeparatedValuesInferenceOptions options,
        SeparatedValuesDialect dialect)
    {
        var builder = new StructuredSchemaBuilder(StructuredTypeConflictBehavior.WidenToString);
        if (request.HasHeader)
        {
            var names = ReadAndDeclareHeader(identity.CanonicalPath, reader, builder, options);
            return ContinueSample(
                request,
                identity,
                reader,
                builder,
                names,
                reader.NextRecordOffset,
                0,
                identityBytes,
                started,
                acquisitionStarted,
                options,
                dialect);
        }

        return ContinueSample(
            request,
            identity,
            reader,
            builder,
            [],
            reader.NextRecordOffset,
            0,
            identityBytes,
            started,
            acquisitionStarted,
            options,
            dialect);
    }

    private static SeparatedValuesSourceContract ContinueSample(
        SeparatedValuesSchemaResolutionRequest request,
        StructuredFileIdentity identity,
        SeparatedValuesUtf8Reader reader,
        StructuredSchemaBuilder builder,
        List<string> names,
        long dataStartOffset,
        int inspectedRows,
        long identityBytes,
        long started,
        long acquisitionStarted,
        SeparatedValuesInferenceOptions options,
        SeparatedValuesDialect dialect)
    {
        var reachedEnd = false;
        var deadline = CreateDeadline(acquisitionStarted, options.MaximumTime);
        var culture = SeparatedValuesValueConverter.GetCulture(dialect.CultureName);

        while (inspectedRows < options.MaximumRows && Stopwatch.GetTimestamp() < deadline)
        {
            if (!reader.TryRead(out var record))
            {
                reachedEnd = !reader.BudgetExhausted;
                break;
            }

            ObserveRecord(record, request.HasHeader, names, builder, culture);
            inspectedRows++;
        }

        if (!request.HasHeader && inspectedRows == 0 && reader.BudgetExhausted)
            throw BudgetExceeded(identity.CanonicalPath, options, "before the complete first record was read");

        var snapshot = MakeConservativelyNullable(
            SeparatedValuesFormat.NormalizeUnresolvedColumns(builder.Build(identity)));
        return new SeparatedValuesSourceContract(
            snapshot,
            SeparatedValuesSchemaResolutionMode.Sampled,
            reachedEnd,
            inspectedRows,
            checked(identityBytes + reader.BytesRead),
            Stopwatch.GetElapsedTime(started),
            dataStartOffset,
            null,
            null,
            dialect);
    }

    private static void ObserveRecord(
        SeparatedValuesUtf8Record record,
        bool hasHeader,
        List<string> names,
        StructuredSchemaBuilder builder,
        IFormatProvider culture)
    {
        builder.BeginRow();
        var fieldIndex = 0;
        foreach (var field in record)
        {
            if (hasHeader && fieldIndex >= names.Count)
            {
                throw new InvalidDataException(
                    $"Separated-values row {builder.RowCount:N0} has more fields than its {names.Count:N0}-column header.");
            }

            if (!hasHeader && fieldIndex == names.Count)
            {
                var name = string.Format(
                    CultureInfo.InvariantCulture,
                    SeparatedValuesHelper.AutoColumnName,
                    fieldIndex + 1);
                names.Add(name);
            }

            builder.Observe(names[fieldIndex], SeparatedValuesFormat.Infer(field, culture));
            fieldIndex++;
        }
    }

    private static List<string> ReadAndDeclareHeader(
        string path,
        SeparatedValuesUtf8Reader reader,
        StructuredSchemaBuilder builder,
        SeparatedValuesInferenceOptions options)
    {
        if (!reader.TryRead(out var header))
        {
            if (reader.BudgetExhausted)
                throw BudgetExceeded(path, options, "before the complete header was read");
            throw new InvalidDataException("A headered separated-values source must contain a non-empty header record.");
        }

        var names = ReadHeaderNames(header).ToList();
        foreach (var name in names)
            builder.DeclareColumn(name);
        return names;
    }

    private static string[] ReadHeaderNames(SeparatedValuesUtf8Record header)
    {
        var names = new List<string>();
        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in header)
        {
            var name = field.Decode();
            if (name.Length == 0)
                throw new InvalidDataException("Separated-values headers cannot be empty.");
            if (!unique.Add(name))
                throw new InvalidDataException($"Separated-values header '{name}' occurs more than once.");
            names.Add(name);
        }

        if (names.Count == 0)
            throw new InvalidDataException("A headered separated-values source must contain at least one column.");
        return names.ToArray();
    }

    private static Type[] BindDeclaredColumns(
        string path,
        string[] sourceNames,
        IReadOnlyCollection<ISchemaColumn> declaredColumns,
        bool hasHeader)
    {
        var ordered = declaredColumns.OrderBy(column => column.ColumnIndex).ToArray();
        if (!hasHeader)
        {
            var positional = ordered.All(column => TryGetAutoColumnOrdinal(column.ColumnName, out _));
            if (!positional && sourceNames.Length != ordered.Length)
            {
                throw new StructuredSchemaDriftException(
                    path,
                    $"the headerless source has {sourceNames.Length:N0} columns but the TABLE contract declares " +
                    $"{ordered.Length:N0}");
            }

            foreach (var declared in ordered)
                ValidateSupportedType(declared.ColumnType);
            var positionalTypes = Enumerable.Repeat(typeof(string), sourceNames.Length).ToArray();
            foreach (var declared in ordered)
            {
                if (!TryGetAutoColumnOrdinal(declared.ColumnName, out var ordinal) ||
                    ordinal <= 0 ||
                    ordinal > sourceNames.Length)
                {
                    throw new StructuredSchemaDriftException(
                        path,
                        $"declared column '{declared.ColumnName}' does not exist in the source shape");
                }

                positionalTypes[ordinal - 1] = declared.ColumnType;
            }
            return positionalTypes;
        }

        var byName = declaredColumns.ToDictionary(column => column.ColumnName, StringComparer.Ordinal);
        var sourceNameSet = sourceNames.ToHashSet(StringComparer.Ordinal);
        foreach (var declared in declaredColumns)
        {
            if (!sourceNameSet.Contains(declared.ColumnName))
            {
                throw new StructuredSchemaDriftException(
                    path,
                    $"declared column '{declared.ColumnName}' does not exist in the source shape");
            }
            ValidateSupportedType(declared.ColumnType);
        }

        var types = new Type[sourceNames.Length];
        for (var index = 0; index < sourceNames.Length; index++)
        {
            types[index] = byName.TryGetValue(sourceNames[index], out var declared)
                ? declared.ColumnType
                : typeof(string);
        }

        return types;
    }

    private static bool TryGetAutoColumnOrdinal(string name, out int ordinal)
    {
        const string prefix = "Column";
        if (!name.StartsWith(prefix, StringComparison.Ordinal) ||
            !int.TryParse(name.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out ordinal) ||
            ordinal <= 0)
        {
            ordinal = 0;
            return false;
        }

        return true;
    }

    private static int CountFields(SeparatedValuesUtf8Record record)
    {
        var count = 0;
        foreach (var _ in record)
            count++;
        return count;
    }

    private static ISchemaColumn[]? GetConcreteDeclaredColumns(IReadOnlyCollection<ISchemaColumn> columns)
    {
        if (columns.Count == 0 || columns.Any(column => column.ColumnType == typeof(object)))
            return null;

        var result = columns.ToArray();
        foreach (var column in result)
            ValidateSupportedType(column.ColumnType);
        return result;
    }

    private static void ValidateSupportedType(Type type)
    {
        _ = SeparatedValuesValueConverter.GetConversion(type, StructuredTypeState.Empty);
    }

    private static StructuredTypeState ToStructuredType(Type declaredType)
    {
        var nullable = Nullable.GetUnderlyingType(declaredType) is not null;
        var type = Nullable.GetUnderlyingType(declaredType) ?? declaredType;
        var kind = type == typeof(bool)
            ? StructuredValueKind.Boolean
            : type == typeof(decimal)
                ? StructuredValueKind.Decimal
                : type == typeof(float) || type == typeof(double)
                    ? StructuredValueKind.Double
                    : type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) ||
                      type == typeof(ushort) || type == typeof(int) || type == typeof(uint) ||
                      type == typeof(long) || type == typeof(ulong)
                        ? StructuredValueKind.Long
                        : StructuredValueKind.String;
        return new StructuredTypeState(kind, nullable);
    }

    private static StructuredSchemaSnapshot MakeConservativelyNullable(StructuredSchemaSnapshot snapshot)
    {
        return new StructuredSchemaSnapshot(
            snapshot.Identity,
            snapshot.Columns.Select(column => column with
            {
                TypeState = column.TypeState with { IsNullable = true }
            }),
            snapshot.RowCount);
    }

    private static long EstimateFingerprintBytes(long fileLength)
    {
        return fileLength <= FingerprintEdgeBytes * 2L
            ? fileLength
            : FingerprintEdgeBytes * 2L;
    }

    private static long CreateDeadline(long started, TimeSpan maximumTime)
    {
        var timestampDelta = maximumTime.TotalSeconds * Stopwatch.Frequency;
        return timestampDelta >= long.MaxValue - started
            ? long.MaxValue
            : started + (long)timestampDelta;
    }

    private static InvalidDataException BudgetExceeded(
        string path,
        SeparatedValuesInferenceOptions options,
        string stage)
    {
        return new InvalidDataException(
            $"Separated-values schema inference for '{path}' exhausted its bounded budget {stage}. " +
            $"Limits: {options.MaximumBytes:N0} bytes, {options.MaximumRows:N0} rows, " +
            $"{options.MaximumTime.TotalMilliseconds:N0} ms. Provide a concrete TABLE contract or raise the " +
            $"'{SeparatedValuesInferenceOptions.MaximumBytesSettingName}', " +
            $"'{SeparatedValuesInferenceOptions.MaximumRowsSettingName}', and " +
            $"'{SeparatedValuesInferenceOptions.MaximumTimeMillisecondsSettingName}' settings.");
    }
}
