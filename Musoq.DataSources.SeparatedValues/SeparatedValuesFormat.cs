#nullable enable

using System;
using System.Buffers.Text;
using System.IO;
using System.Linq;
using System.Globalization;
using Musoq.DataSources.Structured;

namespace Musoq.DataSources.SeparatedValues;

internal static class SeparatedValuesFormat
{
    public static StructuredValueKind Infer(SeparatedValuesUtf8Field field)
    {
        return Infer(field, CultureInfo.InvariantCulture);
    }

    public static StructuredValueKind Infer(
        SeparatedValuesUtf8Field field,
        IFormatProvider culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        var value = field.EncodedValue;
        if (value.IsEmpty || field.IsNullToken)
            return field.WasQuoted ? StructuredValueKind.String : StructuredValueKind.Null;
        if (field.NeedsUnescaping)
            return StructuredValueKind.String;
        if (IsBoolean(value))
            return StructuredValueKind.Boolean;
        if (culture is CultureInfo configuredCulture &&
            !ReferenceEquals(configuredCulture, CultureInfo.InvariantCulture) &&
            (value.IndexOf((byte)',' ) >= 0 || value.IndexOf((byte)'.') >= 0) &&
            decimal.TryParse(
                field.Decode(),
                NumberStyles.Number,
                configuredCulture,
                out _))
            return StructuredValueKind.Decimal;

        if (value.IndexOfAny((byte)'e', (byte)'E') >= 0)
        {
            return Utf8Parser.TryParse(value, out double exponent, out var consumed) &&
                   consumed == value.Length &&
                   double.IsFinite(exponent)
                ? StructuredValueKind.Double
                : StructuredValueKind.String;
        }

        if (value.IndexOf((byte)'.') >= 0)
        {
            if (SeparatedValuesDecimalParser.TryParse(value, out _))
                return StructuredValueKind.Decimal;
            return Utf8Parser.TryParse(value, out double fraction, out var consumed) &&
                   consumed == value.Length &&
                   double.IsFinite(fraction)
                ? StructuredValueKind.Double
                : StructuredValueKind.String;
        }

        return Utf8Parser.TryParse(value, out long _, out var integerConsumed) && integerConsumed == value.Length
            ? StructuredValueKind.Long
            : StructuredValueKind.String;
    }

    public static StructuredSchemaSnapshot NormalizeUnresolvedColumns(StructuredSchemaSnapshot snapshot)
    {
        if (snapshot.Columns.All(column =>
                column.TypeState.Kind is not (StructuredValueKind.Unknown or StructuredValueKind.Null)))
            return snapshot;

        return new StructuredSchemaSnapshot(
            snapshot.Identity,
            snapshot.Columns.Select(column => column.TypeState.Kind is StructuredValueKind.Unknown or StructuredValueKind.Null
                ? column with
                {
                    TypeState = new StructuredTypeState(
                        StructuredValueKind.String,
                        true)
                }
                : column),
            snapshot.RowCount,
            snapshot.Partitions);
    }

    public static byte GetSeparatorByte(string separator)
    {
        ArgumentNullException.ThrowIfNull(separator);
        if (separator.Length != 1 || separator[0] > 0x7f)
            throw new ArgumentException("The separated-values delimiter must be one ASCII character.", nameof(separator));

        var value = checked((byte)separator[0]);
        if (value is (byte)'"' or (byte)'\r' or (byte)'\n')
            throw new ArgumentException("The configured separated-values delimiter is not supported.", nameof(separator));
        return value;
    }

    public static string CreateParserOptions(byte separator, bool hasHeader, int skipLines)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(skipLines);
        return FormattableString.Invariant(
            $"separated-values:strict-utf8:v3;separator={separator};header={hasHeader};skip-lines={skipLines}");
    }

    public static string CreateParserOptions(
        SeparatedValuesDialect dialect,
        bool hasHeader,
        int skipLines)
    {
        ArgumentNullException.ThrowIfNull(dialect);
        ArgumentOutOfRangeException.ThrowIfNegative(skipLines);
        return FormattableString.Invariant(
            $"separated-values:utf8:v4;{dialect.Fingerprint};header={hasHeader};skip-lines={skipLines}");
    }

    private static bool IsBoolean(ReadOnlySpan<byte> value)
    {
        return value.Length switch
        {
            4 => ToLowerAscii(value[0]) == (byte)'t' &&
                 ToLowerAscii(value[1]) == (byte)'r' &&
                 ToLowerAscii(value[2]) == (byte)'u' &&
                 ToLowerAscii(value[3]) == (byte)'e',
            5 => ToLowerAscii(value[0]) == (byte)'f' &&
                 ToLowerAscii(value[1]) == (byte)'a' &&
                 ToLowerAscii(value[2]) == (byte)'l' &&
                 ToLowerAscii(value[3]) == (byte)'s' &&
                 ToLowerAscii(value[4]) == (byte)'e',
            _ => false
        };
    }

    private static byte ToLowerAscii(byte value)
    {
        return value is >= (byte)'A' and <= (byte)'Z'
            ? (byte)(value + ((byte)'a' - (byte)'A'))
            : value;
    }
}
