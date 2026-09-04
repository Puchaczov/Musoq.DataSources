#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Buffers.Text;
using Musoq.Schema;

namespace Musoq.DataSources.SeparatedValues;

/// <summary>
/// Immutable, compilation-scoped decoding plan for one explicit separated-values enum column.
/// </summary>
internal sealed class SeparatedValuesEnumPlan
{
    private SeparatedValuesEnumPlan(
        int sourceOrdinal,
        Type carrierType,
        EnumUnderlyingKind backingKind,
        SeparatedValuesConversion primitiveConversion,
        bool isNullable,
        EnumTypeDescriptor descriptor,
        ImmutableArray<SeparatedValuesEnumNameEntry> names)
    {
        SourceOrdinal = sourceOrdinal;
        CarrierType = carrierType;
        BackingKind = backingKind;
        PrimitiveConversion = primitiveConversion;
        IsNullable = isNullable;
        Descriptor = descriptor;
        Fingerprint = descriptor.Fingerprint;
        Names = names;
    }

    public int SourceOrdinal { get; }

    public Type CarrierType { get; }

    public EnumUnderlyingKind BackingKind { get; }

    public SeparatedValuesConversion PrimitiveConversion { get; }

    public bool IsNullable { get; }

    public EnumTypeDescriptor Descriptor { get; }

    public string Fingerprint { get; }

    public ImmutableArray<SeparatedValuesEnumNameEntry> Names { get; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryDecode(
        SeparatedValuesUtf8Field field,
        out SeparatedValuesParsedValue parsed)
    {
        if (TryParseNumeric(field, out var numeric))
        {
            parsed = SeparatedValuesParsedValue.FromEnum(PrimitiveConversion, numeric);
            return true;
        }

        var hash = HashField(field);
        var first = 0;
        var last = Names.Length;
        while (first < last)
        {
            var middle = first + ((last - first) >> 1);
            if (Names[middle].Hash < hash)
                first = middle + 1;
            else
                last = middle;
        }

        for (var index = first; index < Names.Length && Names[index].Hash == hash; index++)
        {
            if (field.ValueEquals(Names[index].Utf8Name))
            {
                parsed = SeparatedValuesParsedValue.FromEnum(PrimitiveConversion, Names[index].Value);
                return true;
            }
        }

        parsed = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryParseNumeric(SeparatedValuesUtf8Field field, out EnumScalarValue value)
    {
        value = default;
        if (field.NeedsUnescaping)
            return false;

        var encoded = field.EncodedValue;
        if (encoded.IsEmpty)
            return false;

        // All supported integral grammars begin with a sign or an ASCII digit.
        // This keeps symbolic names on the hash path without invoking the more
        // general numeric parser for an obviously non-numeric token.
        var first = encoded[0];
        if ((first < (byte)'0' || first > (byte)'9') && first is not (byte)'+' and not (byte)'-')
            return false;

        switch (PrimitiveConversion)
        {
            case SeparatedValuesConversion.Byte when Utf8Parser.TryParse(encoded, out byte byteValue, out var byteConsumed) && byteConsumed == encoded.Length:
                value = EnumScalarValue.FromByte(byteValue);
                return true;
            case SeparatedValuesConversion.SByte when Utf8Parser.TryParse(encoded, out sbyte sbyteValue, out var sbyteConsumed) && sbyteConsumed == encoded.Length:
                value = EnumScalarValue.FromSByte(sbyteValue);
                return true;
            case SeparatedValuesConversion.Int16 when Utf8Parser.TryParse(encoded, out short int16Value, out var int16Consumed) && int16Consumed == encoded.Length:
                value = EnumScalarValue.FromInt16(int16Value);
                return true;
            case SeparatedValuesConversion.UInt16 when Utf8Parser.TryParse(encoded, out ushort uint16Value, out var uint16Consumed) && uint16Consumed == encoded.Length:
                value = EnumScalarValue.FromUInt16(uint16Value);
                return true;
            case SeparatedValuesConversion.Int32 when Utf8Parser.TryParse(encoded, out int int32Value, out var int32Consumed) && int32Consumed == encoded.Length:
                value = EnumScalarValue.FromInt32(int32Value);
                return true;
            case SeparatedValuesConversion.UInt32 when Utf8Parser.TryParse(encoded, out uint uint32Value, out var uint32Consumed) && uint32Consumed == encoded.Length:
                value = EnumScalarValue.FromUInt32(uint32Value);
                return true;
            case SeparatedValuesConversion.Int64 when Utf8Parser.TryParse(encoded, out long int64Value, out var int64Consumed) && int64Consumed == encoded.Length:
                value = EnumScalarValue.FromInt64(int64Value);
                return true;
            case SeparatedValuesConversion.UInt64 when Utf8Parser.TryParse(encoded, out ulong uint64Value, out var uint64Consumed) && uint64Consumed == encoded.Length:
                value = EnumScalarValue.FromUInt64(uint64Value);
                return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong HashField(SeparatedValuesUtf8Field field)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        var hash = offset;
        var bytes = field.EncodedValue;
        for (var index = 0; index < bytes.Length; index++)
        {
            var value = bytes[index];
            if (field.EscapeMode == SeparatedValuesEscapeMode.Double &&
                field.Quote.HasValue &&
                value == field.Quote.Value &&
                index + 1 < bytes.Length &&
                bytes[index + 1] == field.Quote.Value)
                index++;
            else if (field.EscapeMode == SeparatedValuesEscapeMode.Backslash &&
                     value == (byte)'\\' &&
                     index + 1 < bytes.Length)
                value = bytes[++index];

            hash = (hash ^ value) * prime;
        }

        return hash;
    }

    public static SeparatedValuesEnumPlan Create(
        int sourceOrdinal,
        Type carrierType,
        EnumTypeDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(carrierType);
        ArgumentNullException.ThrowIfNull(descriptor);

        var nullableType = Nullable.GetUnderlyingType(carrierType);
        var primitiveType = nullableType ?? carrierType;
        var expectedCarrier = EnumScalarTypeFacts.GetCarrierType(descriptor.UnderlyingKind);
        if (primitiveType != expectedCarrier)
        {
            throw new ArgumentException(
                $"Enum column carrier '{carrierType}' does not match descriptor backing " +
                $"'{descriptor.UnderlyingKind}'.",
                nameof(carrierType));
        }

        if (!SeparatedValuesValueConverter.TryGetExactConversion(carrierType, out var conversion) ||
            !IsIntegralConversion(conversion))
        {
            throw new ArgumentException(
                $"Enum column carrier '{carrierType}' is not an exact integral separated-values type.",
                nameof(carrierType));
        }

        var entries = descriptor.Members
            .Select(member => new SeparatedValuesEnumNameEntry(
                HashUtf8(Encoding.UTF8.GetBytes(member.Name)),
                Encoding.UTF8.GetBytes(member.Name),
                member.Value))
            .OrderBy(entry => entry.Hash)
            .ThenBy(entry => entry.Utf8Name, ByteArrayComparer.Instance)
            .ToImmutableArray();

        return new SeparatedValuesEnumPlan(
            sourceOrdinal,
            carrierType,
            descriptor.UnderlyingKind,
            conversion,
            nullableType is not null,
            descriptor,
            entries);
    }

    public static ulong HashUtf8(ReadOnlySpan<byte> value)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        var hash = offset;
        foreach (var byteValue in value)
            hash = (hash ^ byteValue) * prime;
        return hash;
    }

    private static bool IsIntegralConversion(SeparatedValuesConversion conversion)
    {
        return conversion is SeparatedValuesConversion.Byte or
            SeparatedValuesConversion.SByte or
            SeparatedValuesConversion.Int16 or
            SeparatedValuesConversion.Int32 or
            SeparatedValuesConversion.Int64 or
            SeparatedValuesConversion.UInt16 or
            SeparatedValuesConversion.UInt32 or
            SeparatedValuesConversion.UInt64;
    }

    private sealed class ByteArrayComparer : IComparer<byte[]>
    {
        public static ByteArrayComparer Instance { get; } = new();

        public int Compare(byte[]? left, byte[]? right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left is null)
                return -1;
            if (right is null)
                return 1;

            var length = Math.Min(left.Length, right.Length);
            for (var index = 0; index < length; index++)
            {
                var comparison = left[index].CompareTo(right[index]);
                if (comparison != 0)
                    return comparison;
            }

            return left.Length.CompareTo(right.Length);
        }
    }
}

internal readonly record struct SeparatedValuesEnumNameEntry(
    ulong Hash,
    byte[] Utf8Name,
    EnumScalarValue Value);
