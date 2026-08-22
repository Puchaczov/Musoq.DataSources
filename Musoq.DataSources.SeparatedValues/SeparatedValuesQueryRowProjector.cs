#nullable enable

using System;
using System.Buffers;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Musoq.Schema;

namespace Musoq.DataSources.SeparatedValues;

internal sealed class SeparatedValuesQueryProjectionPlan
{
    private readonly SeparatedValuesQueryFieldMapping[] _diagnostics;
    private readonly SeparatedValuesQuerySlotBinding[] _slotBindings;

    private SeparatedValuesQueryProjectionPlan(
        string sourcePath,
        string fingerprint,
        int[] sourceOrdinals,
        SeparatedValuesQueryFieldMapping[] diagnostics,
        SeparatedValuesQuerySlotBinding[] slotBindings)
    {
        SourcePath = sourcePath;
        Fingerprint = fingerprint;
        SourceOrdinals = sourceOrdinals;
        _diagnostics = diagnostics;
        _slotBindings = slotBindings;
    }

    public string SourcePath { get; }

    public string Fingerprint { get; }

    public int[] SourceOrdinals { get; }

    public bool HasOutputColumns => _slotBindings.Length > 0;

    public int SlotCount => _slotBindings.Length;

    public static SeparatedValuesQueryProjectionPlan Create(
        SeparatedValuesSourceContract contract,
        SeparatedValuesQueryShapeMapping mapping)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(mapping);

        var captureBindings = mapping.Fields
            .ToArray();
        SortByPhysicalSourceOrdinal(captureBindings);
        var slotBindings = new SeparatedValuesQuerySlotBinding[mapping.Fields.Length];
        var diagnostics = new SeparatedValuesQueryFieldMapping[mapping.Fields.Length];
        var sourceOrdinals = new int[mapping.Fields.Length];
        var assignedSlots = new bool[mapping.Fields.Length];
        for (var captureIndex = 0; captureIndex < captureBindings.Length; captureIndex++)
        {
            var field = captureBindings[captureIndex];
            if ((uint)field.Slot >= (uint)slotBindings.Length)
            {
                throw new InvalidOperationException(
                    $"Separated-values source '{contract.Snapshot.Identity.CanonicalPath}' query shape " +
                    $"'{mapping.Fingerprint}' contains out-of-range slot {field.Slot}.");
            }

            if (assignedSlots[field.Slot])
            {
                throw new InvalidOperationException(
                    $"Separated-values source '{contract.Snapshot.Identity.CanonicalPath}' query shape " +
                    $"'{mapping.Fingerprint}' contains duplicate slot {field.Slot}.");
            }

            assignedSlots[field.Slot] = true;
            sourceOrdinals[captureIndex] = field.PhysicalSourceOrdinal;
            diagnostics[field.Slot] = field;
            slotBindings[field.Slot] = new SeparatedValuesQuerySlotBinding(
                captureIndex,
                field.PhysicalSourceOrdinal,
                field.Conversion,
                field.IsNullable);
        }

        for (var slot = 0; slot < assignedSlots.Length; slot++)
        {
            if (!assignedSlots[slot])
            {
                throw new InvalidOperationException(
                    $"Separated-values source '{contract.Snapshot.Identity.CanonicalPath}' query shape " +
                    $"'{mapping.Fingerprint}' does not define dense slot {slot}.");
            }
        }

        return new SeparatedValuesQueryProjectionPlan(
            contract.Snapshot.Identity.CanonicalPath,
            mapping.Fingerprint,
            sourceOrdinals,
            diagnostics,
            slotBindings);
    }

    public bool HasProjectionAt(int sourceOrdinal)
    {
        return Array.BinarySearch(SourceOrdinals, sourceOrdinal) >= 0;
    }

    public ref readonly SeparatedValuesQuerySlotBinding GetSlotBinding(int slot)
    {
        return ref _slotBindings[slot];
    }

    public ref readonly SeparatedValuesQueryFieldMapping GetDiagnostic(int slot)
    {
        return ref _diagnostics[slot];
    }

    private static void SortByPhysicalSourceOrdinal(SeparatedValuesQueryFieldMapping[] bindings)
    {
        for (var index = 1; index < bindings.Length; index++)
        {
            var current = bindings[index];
            var insertion = index;
            while (insertion > 0 &&
                   bindings[insertion - 1].PhysicalSourceOrdinal > current.PhysicalSourceOrdinal)
            {
                bindings[insertion] = bindings[insertion - 1];
                insertion--;
            }

            bindings[insertion] = current;
        }
    }
}

internal readonly record struct SeparatedValuesQuerySlotBinding(
    int CaptureIndex,
    int PhysicalSourceOrdinal,
    SeparatedValuesConversion Conversion,
    bool IsNullable);

internal readonly struct SeparatedValuesQueryRowProjector<TRow, TMaterializer>
    : ISeparatedValuesRowProjector<TRow>
    where TMaterializer : struct, IQueryRowMaterializer<TRow>
{
    private readonly SeparatedValuesPhysicalFieldTraversal _fields;
    private readonly SeparatedValuesQueryProjectionPlan _projection;

    public SeparatedValuesQueryRowProjector(
        SeparatedValuesPhysicalFieldTraversal fields,
        SeparatedValuesQueryProjectionPlan projection)
    {
        _fields = fields ?? throw new ArgumentNullException(nameof(fields));
        _projection = projection ?? throw new ArgumentNullException(nameof(projection));
    }

    public bool CanRepeatRow => false;

    public TRow RepeatedRow => throw new InvalidOperationException(
        "Query-scoped rows must be materialized for each accepted source row.");

    public TRow Materialize(SeparatedValuesUtf8Record record, long rowNumber)
    {
        _fields.RecordMaterializedRow();
        var reader = new SeparatedValuesFieldReader(record.Bytes, _fields, _projection, rowNumber);
        return TMaterializer.Materialize<SeparatedValuesFieldReader>(ref reader);
    }

    private ref struct SeparatedValuesFieldReader : IQuerySourceFieldReader
    {
        private readonly SeparatedValuesPhysicalFieldTraversal _fields;
        private readonly SeparatedValuesQueryProjectionPlan _projection;
        private readonly ReadOnlySpan<byte> _recordBytes;
        private readonly long _rowNumber;

        public SeparatedValuesFieldReader(
            ReadOnlySpan<byte> recordBytes,
            SeparatedValuesPhysicalFieldTraversal fields,
            SeparatedValuesQueryProjectionPlan projection,
            long rowNumber)
        {
            _recordBytes = recordBytes;
            _fields = fields;
            _projection = projection;
            _rowNumber = rowNumber;
        }

        public T Read<T>(int slot)
        {
            if ((uint)slot >= (uint)_projection.SlotCount)
                throw CreateShapeError($"requested out-of-range slot {slot}");

            ref readonly var slotBinding = ref _projection.GetSlotBinding(slot);
            if (!SeparatedValuesTypedValueReader.IsExact<T>(
                    slotBinding.Conversion,
                    slotBinding.IsNullable))
            {
                ref readonly var diagnostic = ref _projection.GetDiagnostic(slot);
                throw CreateShapeError(
                    $"slot {slot} ('{diagnostic.Name}') requested '{typeof(T)}' instead of '{diagnostic.FieldType}'");
            }

            ref readonly var location = ref _fields.GetLocation(slotBinding.CaptureIndex);
            if (!location.Present)
                return ReadMissing<T>(slot, slotBinding, "is missing from the physical record");

            if (location.IsNull)
                return ReadMissing<T>(slot, slotBinding, "contains a null token");

            try
            {
                if (slotBinding.Conversion == SeparatedValuesConversion.String)
                {
                    var value = location.NeedsUnescaping
                        ? location.CreateField(_recordBytes).Decode()
                        : _fields.StringPool.GetOrAddUtf8(
                            slotBinding.PhysicalSourceOrdinal,
                            location.GetEncodedValue(_recordBytes));
                    return Unsafe.As<string, T>(ref value);
                }

                return SeparatedValuesTypedValueReader.Read<T>(
                    _recordBytes,
                    in location,
                    slotBinding.PhysicalSourceOrdinal,
                    _fields.Culture,
                    _fields.StringPool);
            }
            catch (Exception exception) when (exception is FormatException or OverflowException)
            {
                ref readonly var diagnostic = ref _projection.GetDiagnostic(slot);
                throw new FormatException(
                    $"Separated-values row {_rowNumber:N0} column '{diagnostic.Name}' " +
                    $"cannot be converted as {slotBinding.Conversion}.",
                    exception);
            }
        }

        private T ReadMissing<T>(int slot, SeparatedValuesQuerySlotBinding binding, string reason)
        {
            if (binding.IsNullable)
                return default!;

            ref readonly var diagnostic = ref _projection.GetDiagnostic(slot);
            throw new FormatException(
                $"Separated-values source '{_projection.SourcePath}' row {_rowNumber:N0} " +
                $"column '{diagnostic.Name}' {reason}, but query shape '{_projection.Fingerprint}' " +
                $"requires non-nullable '{diagnostic.FieldType}'.");
        }

        private InvalidOperationException CreateShapeError(string reason)
        {
            return new InvalidOperationException(
                $"Separated-values source '{_projection.SourcePath}' cannot materialize query shape " +
                $"'{_projection.Fingerprint}': {reason}.");
        }
    }
}

internal static class SeparatedValuesTypedValueReader
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsExact<T>(SeparatedValuesConversion conversion, bool isNullable)
    {
        if (conversion == SeparatedValuesConversion.String)
            return typeof(T) == typeof(string);

        if (typeof(T) == typeof(bool)) return conversion == SeparatedValuesConversion.Boolean && !isNullable;
        if (typeof(T) == typeof(bool?)) return conversion == SeparatedValuesConversion.Boolean && isNullable;
        if (typeof(T) == typeof(byte)) return conversion == SeparatedValuesConversion.Byte && !isNullable;
        if (typeof(T) == typeof(byte?)) return conversion == SeparatedValuesConversion.Byte && isNullable;
        if (typeof(T) == typeof(sbyte)) return conversion == SeparatedValuesConversion.SByte && !isNullable;
        if (typeof(T) == typeof(sbyte?)) return conversion == SeparatedValuesConversion.SByte && isNullable;
        if (typeof(T) == typeof(short)) return conversion == SeparatedValuesConversion.Int16 && !isNullable;
        if (typeof(T) == typeof(short?)) return conversion == SeparatedValuesConversion.Int16 && isNullable;
        if (typeof(T) == typeof(int)) return conversion == SeparatedValuesConversion.Int32 && !isNullable;
        if (typeof(T) == typeof(int?)) return conversion == SeparatedValuesConversion.Int32 && isNullable;
        if (typeof(T) == typeof(long)) return conversion == SeparatedValuesConversion.Int64 && !isNullable;
        if (typeof(T) == typeof(long?)) return conversion == SeparatedValuesConversion.Int64 && isNullable;
        if (typeof(T) == typeof(ushort)) return conversion == SeparatedValuesConversion.UInt16 && !isNullable;
        if (typeof(T) == typeof(ushort?)) return conversion == SeparatedValuesConversion.UInt16 && isNullable;
        if (typeof(T) == typeof(uint)) return conversion == SeparatedValuesConversion.UInt32 && !isNullable;
        if (typeof(T) == typeof(uint?)) return conversion == SeparatedValuesConversion.UInt32 && isNullable;
        if (typeof(T) == typeof(ulong)) return conversion == SeparatedValuesConversion.UInt64 && !isNullable;
        if (typeof(T) == typeof(ulong?)) return conversion == SeparatedValuesConversion.UInt64 && isNullable;
        if (typeof(T) == typeof(decimal)) return conversion == SeparatedValuesConversion.Decimal && !isNullable;
        if (typeof(T) == typeof(decimal?)) return conversion == SeparatedValuesConversion.Decimal && isNullable;
        if (typeof(T) == typeof(float)) return conversion == SeparatedValuesConversion.Single && !isNullable;
        if (typeof(T) == typeof(float?)) return conversion == SeparatedValuesConversion.Single && isNullable;
        if (typeof(T) == typeof(double)) return conversion == SeparatedValuesConversion.Double && !isNullable;
        if (typeof(T) == typeof(double?)) return conversion == SeparatedValuesConversion.Double && isNullable;
        if (typeof(T) == typeof(char)) return conversion == SeparatedValuesConversion.Character && !isNullable;
        if (typeof(T) == typeof(char?)) return conversion == SeparatedValuesConversion.Character && isNullable;
        if (typeof(T) == typeof(DateTime)) return conversion == SeparatedValuesConversion.DateTime && !isNullable;
        if (typeof(T) == typeof(DateTime?)) return conversion == SeparatedValuesConversion.DateTime && isNullable;
        if (typeof(T) == typeof(DateTimeOffset)) return conversion == SeparatedValuesConversion.DateTimeOffset && !isNullable;
        if (typeof(T) == typeof(DateTimeOffset?)) return conversion == SeparatedValuesConversion.DateTimeOffset && isNullable;
        if (typeof(T) == typeof(TimeSpan)) return conversion == SeparatedValuesConversion.TimeSpan && !isNullable;
        if (typeof(T) == typeof(TimeSpan?)) return conversion == SeparatedValuesConversion.TimeSpan && isNullable;
        if (typeof(T) == typeof(Guid)) return conversion == SeparatedValuesConversion.Guid && !isNullable;
        if (typeof(T) == typeof(Guid?)) return conversion == SeparatedValuesConversion.Guid && isNullable;
        if (typeof(T) == typeof(DateOnly)) return conversion == SeparatedValuesConversion.DateOnly && !isNullable;
        if (typeof(T) == typeof(DateOnly?)) return conversion == SeparatedValuesConversion.DateOnly && isNullable;
        if (typeof(T) == typeof(TimeOnly)) return conversion == SeparatedValuesConversion.TimeOnly && !isNullable;
        return typeof(T) == typeof(TimeOnly?) && conversion == SeparatedValuesConversion.TimeOnly && isNullable;
    }

    public static T Read<T>(
        ReadOnlySpan<byte> recordBytes,
        in SeparatedValuesFieldLocation location,
        int physicalSourceOrdinal,
        IFormatProvider culture,
        Structured.StructuredStringPool stringPool)
    {
        var field = location.CreateField(recordBytes);
        var parsed = location.Parsed;

        if (typeof(T) == typeof(bool) || typeof(T) == typeof(bool?))
        {
            var value = parsed.CanCompare(SeparatedValuesConversion.Boolean)
                ? parsed.Boolean
                : ParseBoolean(field);
            return CastValue<bool, T>(value, typeof(T) == typeof(bool?));
        }
        if (typeof(T) == typeof(byte) || typeof(T) == typeof(byte?))
        {
            var value = parsed.CanCompare(SeparatedValuesConversion.Byte) ? parsed.Byte : ParseByte(field);
            return CastValue<byte, T>(value, typeof(T) == typeof(byte?));
        }
        if (typeof(T) == typeof(sbyte) || typeof(T) == typeof(sbyte?))
        {
            var value = parsed.CanCompare(SeparatedValuesConversion.SByte) ? parsed.SByte : ParseSByte(field);
            return CastValue<sbyte, T>(value, typeof(T) == typeof(sbyte?));
        }
        if (typeof(T) == typeof(short) || typeof(T) == typeof(short?))
        {
            var value = parsed.CanCompare(SeparatedValuesConversion.Int16) ? parsed.Int16 : ParseInt16(field);
            return CastValue<short, T>(value, typeof(T) == typeof(short?));
        }
        if (typeof(T) == typeof(int) || typeof(T) == typeof(int?))
        {
            var value = parsed.CanCompare(SeparatedValuesConversion.Int32) ? parsed.Int32 : ParseInt32(field);
            return CastValue<int, T>(value, typeof(T) == typeof(int?));
        }
        if (typeof(T) == typeof(long) || typeof(T) == typeof(long?))
        {
            var value = parsed.CanCompare(SeparatedValuesConversion.Int64) ? parsed.Int64 : ParseInt64(field);
            return CastValue<long, T>(value, typeof(T) == typeof(long?));
        }
        if (typeof(T) == typeof(ushort) || typeof(T) == typeof(ushort?))
        {
            var value = parsed.CanCompare(SeparatedValuesConversion.UInt16) ? parsed.UInt16 : ParseUInt16(field);
            return CastValue<ushort, T>(value, typeof(T) == typeof(ushort?));
        }
        if (typeof(T) == typeof(uint) || typeof(T) == typeof(uint?))
        {
            var value = parsed.CanCompare(SeparatedValuesConversion.UInt32) ? parsed.UInt32 : ParseUInt32(field);
            return CastValue<uint, T>(value, typeof(T) == typeof(uint?));
        }
        if (typeof(T) == typeof(ulong) || typeof(T) == typeof(ulong?))
        {
            var value = parsed.CanCompare(SeparatedValuesConversion.UInt64) ? parsed.UInt64 : ParseUInt64(field);
            return CastValue<ulong, T>(value, typeof(T) == typeof(ulong?));
        }
        if (typeof(T) == typeof(decimal) || typeof(T) == typeof(decimal?))
        {
            var value = parsed.CanCompare(SeparatedValuesConversion.Decimal)
                ? parsed.Decimal
                : ParseDecimal(field, culture);
            return CastValue<decimal, T>(value, typeof(T) == typeof(decimal?));
        }
        if (typeof(T) == typeof(float) || typeof(T) == typeof(float?))
        {
            var value = parsed.CanCompare(SeparatedValuesConversion.Single) ? parsed.Single : ParseSingle(field);
            return CastValue<float, T>(value, typeof(T) == typeof(float?));
        }
        if (typeof(T) == typeof(double) || typeof(T) == typeof(double?))
        {
            var value = parsed.CanCompare(SeparatedValuesConversion.Double) ? parsed.Double : ParseDouble(field);
            return CastValue<double, T>(value, typeof(T) == typeof(double?));
        }
        if (typeof(T) == typeof(char) || typeof(T) == typeof(char?))
            return CastValue<char, T>(ParseCharacter(field), typeof(T) == typeof(char?));
        if (typeof(T) == typeof(DateTime) || typeof(T) == typeof(DateTime?))
            return CastValue<DateTime, T>(ParseDateTime(field, culture), typeof(T) == typeof(DateTime?));
        if (typeof(T) == typeof(DateTimeOffset) || typeof(T) == typeof(DateTimeOffset?))
        {
            return CastValue<DateTimeOffset, T>(
                ParseDateTimeOffset(field, culture),
                typeof(T) == typeof(DateTimeOffset?));
        }
        if (typeof(T) == typeof(TimeSpan) || typeof(T) == typeof(TimeSpan?))
            return CastValue<TimeSpan, T>(ParseTimeSpan(field, culture), typeof(T) == typeof(TimeSpan?));
        if (typeof(T) == typeof(Guid) || typeof(T) == typeof(Guid?))
            return CastValue<Guid, T>(ParseGuid(field), typeof(T) == typeof(Guid?));
        if (typeof(T) == typeof(DateOnly) || typeof(T) == typeof(DateOnly?))
            return CastValue<DateOnly, T>(ParseDateOnly(field, culture), typeof(T) == typeof(DateOnly?));
        if (typeof(T) == typeof(TimeOnly) || typeof(T) == typeof(TimeOnly?))
            return CastValue<TimeOnly, T>(ParseTimeOnly(field, culture), typeof(T) == typeof(TimeOnly?));

        throw new InvalidOperationException(
            $"Separated-values query field type '{typeof(T)}' is not supported.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T Cast<TValue, T>(TValue value)
    {
        return Unsafe.As<TValue, T>(ref value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T CastValue<TValue, T>(TValue value, bool nullable)
        where TValue : struct
    {
        if (!nullable)
            return Cast<TValue, T>(value);

        TValue? nullableValue = value;
        return Cast<TValue?, T>(nullableValue);
    }

    private static bool ParseBoolean(SeparatedValuesUtf8Field field) =>
        SeparatedValuesValueConverter.TryParse(field, out bool value) ? value : throw new FormatException();

    private static byte ParseByte(SeparatedValuesUtf8Field field) =>
        SeparatedValuesValueConverter.TryParse(field, out byte value) ? value : throw new FormatException();

    private static sbyte ParseSByte(SeparatedValuesUtf8Field field) =>
        SeparatedValuesValueConverter.TryParse(field, out sbyte value) ? value : throw new FormatException();

    private static short ParseInt16(SeparatedValuesUtf8Field field) =>
        SeparatedValuesValueConverter.TryParse(field, out short value) ? value : throw new FormatException();

    private static int ParseInt32(SeparatedValuesUtf8Field field) =>
        SeparatedValuesValueConverter.TryParse(field, out int value) ? value : throw new FormatException();

    private static long ParseInt64(SeparatedValuesUtf8Field field) =>
        SeparatedValuesValueConverter.TryParse(field, out long value) ? value : throw new FormatException();

    private static ushort ParseUInt16(SeparatedValuesUtf8Field field) =>
        SeparatedValuesValueConverter.TryParse(field, out ushort value) ? value : throw new FormatException();

    private static uint ParseUInt32(SeparatedValuesUtf8Field field) =>
        SeparatedValuesValueConverter.TryParse(field, out uint value) ? value : throw new FormatException();

    private static ulong ParseUInt64(SeparatedValuesUtf8Field field) =>
        SeparatedValuesValueConverter.TryParse(field, out ulong value) ? value : throw new FormatException();

    private static decimal ParseDecimal(SeparatedValuesUtf8Field field, IFormatProvider culture)
    {
        if (!field.NeedsUnescaping &&
            culture is CultureInfo currentCulture &&
            ReferenceEquals(currentCulture, CultureInfo.InvariantCulture))
        {
            return SeparatedValuesValueConverter.TryParse(field, out decimal invariantValue, culture)
                ? invariantValue
                : throw new FormatException();
        }

        using var decoded = new SeparatedValuesDecodedCharBuffer(field);
        return decimal.TryParse(decoded.Chars, NumberStyles.Number, culture, out var value)
            ? value
            : throw new FormatException();
    }

    private static float ParseSingle(SeparatedValuesUtf8Field field) =>
        SeparatedValuesValueConverter.TryParse(field, out float value) ? value : throw new FormatException();

    private static double ParseDouble(SeparatedValuesUtf8Field field) =>
        SeparatedValuesValueConverter.TryParse(field, out double value) ? value : throw new FormatException();

    private static char ParseCharacter(SeparatedValuesUtf8Field field)
    {
        using var decoded = new SeparatedValuesDecodedCharBuffer(field);
        return decoded.Chars.Length == 1 ? decoded.Chars[0] : throw new FormatException();
    }

    private static DateTime ParseDateTime(SeparatedValuesUtf8Field field, IFormatProvider culture)
    {
        using var decoded = new SeparatedValuesDecodedCharBuffer(field);
        return DateTime.TryParse(decoded.Chars, culture, DateTimeStyles.None, out var value)
            ? value
            : throw new FormatException();
    }

    private static DateTimeOffset ParseDateTimeOffset(SeparatedValuesUtf8Field field, IFormatProvider culture)
    {
        using var decoded = new SeparatedValuesDecodedCharBuffer(field);
        return DateTimeOffset.TryParse(decoded.Chars, culture, DateTimeStyles.None, out var value)
            ? value
            : throw new FormatException();
    }

    private static TimeSpan ParseTimeSpan(SeparatedValuesUtf8Field field, IFormatProvider culture)
    {
        using var decoded = new SeparatedValuesDecodedCharBuffer(field);
        return TimeSpan.TryParse(decoded.Chars, culture, out var value) ? value : throw new FormatException();
    }

    private static Guid ParseGuid(SeparatedValuesUtf8Field field)
    {
        if (!field.NeedsUnescaping && Guid.TryParse(field.EncodedValue, out var utf8Value))
            return utf8Value;

        using var decoded = new SeparatedValuesDecodedCharBuffer(field);
        return Guid.TryParse(decoded.Chars, out var value) ? value : throw new FormatException();
    }

    private static DateOnly ParseDateOnly(SeparatedValuesUtf8Field field, IFormatProvider culture)
    {
        using var decoded = new SeparatedValuesDecodedCharBuffer(field);
        return DateOnly.TryParse(decoded.Chars, culture, DateTimeStyles.None, out var value)
            ? value
            : throw new FormatException();
    }

    private static TimeOnly ParseTimeOnly(SeparatedValuesUtf8Field field, IFormatProvider culture)
    {
        using var decoded = new SeparatedValuesDecodedCharBuffer(field);
        return TimeOnly.TryParse(decoded.Chars, culture, DateTimeStyles.None, out var value)
            ? value
            : throw new FormatException();
    }
}

internal ref struct SeparatedValuesDecodedCharBuffer
{
    private byte[]? _rentedBytes;
    private char[]? _rentedChars;

    public SeparatedValuesDecodedCharBuffer(SeparatedValuesUtf8Field field)
    {
        _rentedBytes = null;
        _rentedChars = null;
        Chars = default;
        ReadOnlySpan<byte> bytes = field.EncodedValue;
        if (field.NeedsUnescaping)
        {
            _rentedBytes = ArrayPool<byte>.Shared.Rent(Math.Max(1, bytes.Length));
            var written = Unescape(field, _rentedBytes);
            bytes = _rentedBytes.AsSpan(0, written);
        }

        var charCount = Encoding.UTF8.GetCharCount(bytes);
        _rentedChars = ArrayPool<char>.Shared.Rent(Math.Max(1, charCount));
        var charsWritten = Encoding.UTF8.GetChars(bytes, _rentedChars);
        Chars = _rentedChars.AsSpan(0, charsWritten);
    }

    public ReadOnlySpan<char> Chars { get; private set; }

    public void Dispose()
    {
        Chars = default;
        if (_rentedBytes is not null)
        {
            ArrayPool<byte>.Shared.Return(_rentedBytes);
            _rentedBytes = null;
        }

        if (_rentedChars is not null)
        {
            ArrayPool<char>.Shared.Return(_rentedChars);
            _rentedChars = null;
        }
    }

    private static int Unescape(SeparatedValuesUtf8Field field, Span<byte> destination)
    {
        var source = field.EncodedValue;
        var written = 0;
        for (var offset = 0; offset < source.Length; offset++)
        {
            var value = source[offset];
            if (field.EscapeMode == SeparatedValuesEscapeMode.Double &&
                field.Quote.HasValue &&
                value == field.Quote.Value &&
                offset + 1 < source.Length &&
                source[offset + 1] == field.Quote.Value)
            {
                offset++;
            }
            else if (field.EscapeMode == SeparatedValuesEscapeMode.Backslash &&
                     value == (byte)'\\' &&
                     offset + 1 < source.Length)
            {
                value = source[++offset];
            }

            destination[written++] = value;
        }

        return written;
    }
}

internal readonly record struct SeparatedValuesQueryOutputMemoryEstimator(
    long PerRowCarrierBytes,
    int StringFieldCount)
{
    private const long ClassHeaderBytes = 16;
    private const long ListReferenceBytes = 8;
    private const long ResultOverheadBytes = 256;
    private const long StringHeaderBytes = 24;

    public static SeparatedValuesQueryOutputMemoryEstimator Create<TRow>(QueryRowShape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        var stringFields = 0;
        long classPayloadBytes = 0;
        foreach (var field in shape.Fields)
        {
            if (field.FieldType == typeof(string))
                stringFields++;
            classPayloadBytes = SaturatingAdd(classPayloadBytes, EstimateFieldStorage(field.FieldType));
        }

        var perRowCarrierBytes = typeof(TRow).IsValueType
            ? AlignToEight(Unsafe.SizeOf<TRow>())
            : SaturatingAdd(
                ListReferenceBytes,
                AlignToEight(SaturatingAdd(ClassHeaderBytes, classPayloadBytes)));
        perRowCarrierBytes = SaturatingAdd(
            perRowCarrierBytes,
            SaturatingMultiply(stringFields, StringHeaderBytes));
        return new SeparatedValuesQueryOutputMemoryEstimator(perRowCarrierBytes, stringFields);
    }

    public long Estimate(long rowCount, long encodedBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rowCount);
        ArgumentOutOfRangeException.ThrowIfNegative(encodedBytes);
        var estimate = SaturatingAdd(
            ResultOverheadBytes,
            SaturatingMultiply(rowCount, PerRowCarrierBytes));
        return StringFieldCount == 0
            ? estimate
            : SaturatingAdd(estimate, SaturatingMultiply(encodedBytes, 2));
    }

    private static long EstimateFieldStorage(Type type)
    {
        if (!type.IsValueType)
            return IntPtr.Size;

        var nullableType = Nullable.GetUnderlyingType(type);
        var valueType = nullableType ?? type;
        long bytes = valueType == typeof(bool) || valueType == typeof(byte) || valueType == typeof(sbyte)
            ? 1
            : valueType == typeof(char) || valueType == typeof(short) || valueType == typeof(ushort)
                ? 2
                : valueType == typeof(int) || valueType == typeof(uint) || valueType == typeof(float) ||
                  valueType == typeof(DateOnly)
                    ? 4
                    : valueType == typeof(decimal) || valueType == typeof(Guid)
                        ? 16
                        : 8;
        return nullableType is null ? bytes : SaturatingAdd(bytes, 8);
    }

    private static long AlignToEight(long value)
    {
        return value >= long.MaxValue - 7 ? long.MaxValue : (value + 7) & ~7L;
    }

    private static long SaturatingAdd(long left, long right)
    {
        return left > long.MaxValue - right ? long.MaxValue : left + right;
    }

    private static long SaturatingMultiply(long left, long right)
    {
        if (left == 0 || right == 0)
            return 0;
        return left > long.MaxValue / right ? long.MaxValue : left * right;
    }
}
