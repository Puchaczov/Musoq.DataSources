#nullable enable

using System;
using System.IO;

namespace Musoq.DataSources.SeparatedValues;

internal static class SeparatedValuesFramingKernel
{
    public static void ValidateRecord(
        ReadOnlySpan<byte> bytes,
        byte separator,
        int expectedWidth,
        string path,
        long rowNumber,
        long absoluteOffset)
    {
        Validate(bytes, separator, expectedWidth, path, rowNumber - 1, absoluteOffset, false);
    }

    public static long ValidateTerminatedRecords(
        ReadOnlySpan<byte> bytes,
        byte separator,
        int expectedWidth,
        string path,
        long startRow,
        long absoluteOffset)
    {
        return Validate(bytes, separator, expectedWidth, path, startRow, absoluteOffset, true);
    }

    private static long Validate(
        ReadOnlySpan<byte> bytes,
        byte separator,
        int expectedWidth,
        string path,
        long startRow,
        long absoluteOffset,
        bool requireTerminated)
    {
        SeparatedValuesUtf8Reader.ValidateUtf8(bytes);
        var state = State.FieldStart;
        var row = startRow;
        var fields = 1;
        var hasRecordBytes = false;

        for (var offset = 0; offset < bytes.Length; offset++)
        {
            var value = bytes[offset];
            switch (state)
            {
                case State.FieldStart:
                    if (value == (byte)'"')
                    {
                        hasRecordBytes = true;
                        state = State.Quoted;
                    }
                    else if (value == separator)
                    {
                        hasRecordBytes = true;
                        fields++;
                    }
                    else if (value == (byte)'\n')
                    {
                        CompleteRecord();
                    }
                    else if (value == (byte)'\r')
                    {
                        if (!TryConsumeCarriageReturn(bytes, ref offset))
                            throw Invalid("A carriage return outside a quoted field must be followed by a line feed.", offset);
                        CompleteRecord();
                    }
                    else
                    {
                        hasRecordBytes = true;
                        state = State.Unquoted;
                    }
                    break;

                case State.Unquoted:
                    if (value == separator)
                    {
                        fields++;
                        state = State.FieldStart;
                    }
                    else if (value == (byte)'\n')
                    {
                        CompleteRecord();
                    }
                    else if (value == (byte)'\r')
                    {
                        if (!TryConsumeCarriageReturn(bytes, ref offset))
                            throw Invalid("A carriage return outside a quoted field must be followed by a line feed.", offset);
                        CompleteRecord();
                    }
                    else if (value == (byte)'"')
                    {
                        throw Invalid("A quote may only appear at the beginning of a field.", offset);
                    }
                    break;

                case State.Quoted:
                    if (value == (byte)'"')
                        state = State.AfterQuote;
                    break;

                case State.AfterQuote:
                    if (value == (byte)'"')
                    {
                        state = State.Quoted;
                    }
                    else if (value == separator)
                    {
                        fields++;
                        state = State.FieldStart;
                    }
                    else if (value == (byte)'\n')
                    {
                        CompleteRecord();
                    }
                    else if (value == (byte)'\r')
                    {
                        if (!TryConsumeCarriageReturn(bytes, ref offset))
                            throw Invalid("A carriage return outside a quoted field must be followed by a line feed.", offset);
                        CompleteRecord();
                    }
                    else
                    {
                        throw Invalid("Only a separator or record ending may follow a closing quote.", offset);
                    }
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported framing state '{state}'.");
            }
        }

        if (state == State.Quoted)
            throw Invalid("The final quoted field is not terminated.", bytes.Length);
        if (requireTerminated && (hasRecordBytes || state != State.FieldStart))
            throw Invalid("A framed block ended in the middle of a record.", bytes.Length);
        if (!requireTerminated && hasRecordBytes)
            CompleteRecord();
        return row - startRow;

        void CompleteRecord()
        {
            if (hasRecordBytes)
            {
                row++;
                if (fields > expectedWidth)
                {
                    throw new InvalidDataException(
                        $"Separated-values source '{path}' row {row:N0} contains more than the bound " +
                        $"{expectedWidth:N0} columns. Byte offset: {absoluteOffset:N0}.");
                }
            }

            state = State.FieldStart;
            fields = 1;
            hasRecordBytes = false;
        }

        InvalidDataException Invalid(string message, int offset)
        {
            return new InvalidDataException(
                $"{message} Source: '{path}', row {row + 1:N0}, byte offset {absoluteOffset + offset:N0}.");
        }
    }

    private static bool TryConsumeCarriageReturn(ReadOnlySpan<byte> bytes, ref int offset)
    {
        if (offset + 1 >= bytes.Length || bytes[offset + 1] != (byte)'\n')
            return false;
        offset++;
        return true;
    }

    private enum State : byte
    {
        FieldStart,
        Unquoted,
        Quoted,
        AfterQuote
    }
}
