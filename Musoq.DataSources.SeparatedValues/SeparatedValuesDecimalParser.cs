#nullable enable

using System;
using System.Buffers.Text;

namespace Musoq.DataSources.SeparatedValues;

internal static class SeparatedValuesDecimalParser
{
    public static bool TryParse(ReadOnlySpan<byte> value, out decimal result)
    {
        if (TryParseCompact(value, out result))
            return true;

        return Utf8Parser.TryParse(value, out result, out var consumed) && consumed == value.Length;
    }

    private static bool TryParseCompact(ReadOnlySpan<byte> value, out decimal result)
    {
        result = default;
        if (value.IsEmpty)
            return false;

        var offset = 0;
        var negative = false;
        if (value[0] is (byte)'-' or (byte)'+')
        {
            negative = value[0] == (byte)'-';
            offset++;
            if (offset == value.Length)
                return false;
        }

        ulong significand = 0;
        var scale = 0;
        var digits = 0;
        var decimalPointSeen = false;

        for (; offset < value.Length; offset++)
        {
            var current = value[offset];
            if (current == (byte)'.' && !decimalPointSeen)
            {
                decimalPointSeen = true;
                continue;
            }

            var digit = (ulong)(current - (byte)'0');
            if (digit > 9)
                return false;
            if (significand > (ulong.MaxValue - digit) / 10)
                return false;

            significand = significand * 10 + digit;
            digits++;
            if (decimalPointSeen && ++scale > 28)
                return false;
        }

        if (!decimalPointSeen || digits == 0)
            return false;

        result = new decimal(
            unchecked((int)significand),
            unchecked((int)(significand >> 32)),
            0,
            negative,
            (byte)scale);
        return true;
    }
}
