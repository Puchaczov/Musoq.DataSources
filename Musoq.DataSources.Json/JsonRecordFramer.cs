#nullable enable

using System;
using System.Buffers;
using System.IO;
using System.Threading;
using Musoq.DataSources.Structured;

namespace Musoq.DataSources.Json;

internal static class JsonRecordFramer
{
    private const int InputBufferSize = 1024 * 1024;
    private static readonly SearchValues<byte> RecordStructuralBytes = SearchValues.Create("\"{}[]"u8);
    private static readonly SearchValues<byte> StringStructuralBytes = SearchValues.Create("\"\\"u8);

    public static void Read(
        string path,
        JsonRowProcessor processor,
        CancellationToken cancellationToken)
    {
        using var stream = OpenAfterOptionalBom(path);
        ReadCore(stream, null, false, processor, cancellationToken);
    }

    public static void ReadPartition(
        string path,
        StructuredPartition partition,
        JsonRowProcessor processor,
        CancellationToken cancellationToken)
    {
        using var stream = OpenRange(path, partition.StartOffset, partition.EndOffset);
        ReadCore(stream, partition.Length, true, processor, cancellationToken);
    }

    private static void ReadCore(
        FileStream stream,
        long? rangeLength,
        bool partitionMode,
        JsonRowProcessor processor,
        CancellationToken cancellationToken)
    {
        var input = ArrayPool<byte>.Shared.Rent(InputBufferSize);
        byte[]? record = null;
        var recordLength = 0;
        var inRecord = false;
        var inString = false;
        var escaped = false;
        var rootSeen = partitionMode;
        var rootIsArray = partitionMode;
        var rootComplete = false;
        var depth = 0;
        var recordStart = 0;
        var recordsSeen = 0L;
        var remaining = rangeLength ?? long.MaxValue;

        try
        {
            while (!rootComplete)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var requested = rangeLength.HasValue
                    ? (int)Math.Min(input.Length, remaining)
                    : input.Length;
                if (requested == 0)
                    break;

                var read = stream.Read(input, 0, requested);
                if (read == 0)
                    break;
                if (rangeLength.HasValue)
                    remaining -= read;

                var index = 0;
                while (index < read)
                {
                    if ((index & 0x3fff) == 0)
                        cancellationToken.ThrowIfCancellationRequested();
                    var value = input[index];

                    if (!rootSeen)
                    {
                        if (IsWhitespace(value))
                        {
                            index++;
                            continue;
                        }

                        rootSeen = true;
                        if (value == (byte)'[')
                        {
                            rootIsArray = true;
                            index++;
                            continue;
                        }

                        if (value != (byte)'{')
                            throw new InvalidDataException("The JSON root must be an object or an array of objects.");

                        StartRecord(index);
                        index++;
                        continue;
                    }

                    if (!inRecord)
                    {
                        if (IsWhitespace(value) || value == (byte)',')
                        {
                            index++;
                            continue;
                        }

                        if (!partitionMode && rootIsArray && value == (byte)']')
                        {
                            rootComplete = true;
                            index++;
                            continue;
                        }

                        if (!rootIsArray)
                            throw new InvalidDataException("JSON input contains more than one root document.");
                        if (value != (byte)'{')
                            throw new InvalidDataException("A JSON root array may contain objects only.");

                        StartRecord(index);
                        index++;
                        continue;
                    }

                    if (inString)
                    {
                        if (escaped)
                        {
                            escaped = false;
                            index++;
                            continue;
                        }

                        var structuralOffset = input.AsSpan(index, read - index).IndexOfAny(StringStructuralBytes);
                        if (structuralOffset < 0)
                        {
                            index = read;
                            continue;
                        }

                        index += structuralOffset;
                        value = input[index];
                        if (value == (byte)'\\')
                            escaped = true;
                        else
                        {
                            inString = false;
                        }

                        index++;
                        continue;
                    }

                    var recordStructuralOffset = input.AsSpan(index, read - index).IndexOfAny(RecordStructuralBytes);
                    if (recordStructuralOffset < 0)
                    {
                        index = read;
                        continue;
                    }

                    index += recordStructuralOffset;
                    value = input[index];

                    switch (value)
                    {
                        case (byte)'"':
                            inString = true;
                            break;
                        case (byte)'{':
                        case (byte)'[':
                            depth++;
                            break;
                        case (byte)'}':
                        case (byte)']':
                            depth--;
                            if (depth == 0)
                            {
                                var continueReading = CompleteRecord(input, recordStart, index + 1);
                                inRecord = false;
                                recordStart = index + 1;
                                recordsSeen++;
                                if (!continueReading)
                                    return;
                                if (!rootIsArray)
                                    rootComplete = true;
                            }

                            break;
                    }

                    index++;
                }

                if (inRecord)
                {
                    AppendRecordBytes(input.AsSpan(recordStart, read - recordStart));
                    recordStart = 0;
                }
            }

            if (partitionMode)
            {
                if (remaining != 0 || inRecord || recordsSeen == 0)
                    throw new InvalidDataException("A JSON partition does not contain complete object records.");
                return;
            }

            if (!rootSeen || !rootComplete || inRecord)
                throw new InvalidDataException("JSON input does not contain one complete root document.");
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(input);
            if (record is not null)
                ArrayPool<byte>.Shared.Return(record);
        }

        void StartRecord(int start)
        {
            inRecord = true;
            inString = false;
            escaped = false;
            depth = 1;
            recordStart = start;
            recordLength = 0;
        }

        bool CompleteRecord(byte[] source, int start, int end)
        {
            if (recordLength == 0)
                return processor.Process(source.AsSpan(start, end - start));

            AppendRecordBytes(source.AsSpan(start, end - start));
            var continueReading = processor.Process(record.AsSpan(0, recordLength));
            recordLength = 0;
            return continueReading;
        }

        void AppendRecordBytes(ReadOnlySpan<byte> bytes)
        {
            var required = checked(recordLength + bytes.Length);
            if (record is null || required > record.Length)
            {
                var newLength = record is null ? InputBufferSize : record.Length;
                while (newLength < required)
                    newLength = checked(newLength * 2);

                var expanded = ArrayPool<byte>.Shared.Rent(newLength);
                if (recordLength > 0)
                    record!.AsSpan(0, recordLength).CopyTo(expanded);
                if (record is not null)
                    ArrayPool<byte>.Shared.Return(record);
                record = expanded;
            }

            bytes.CopyTo(record.AsSpan(recordLength));
            recordLength = required;
        }
    }

    private static FileStream OpenAfterOptionalBom(string path)
    {
        var stream = Open(path);
        Span<byte> preamble = stackalloc byte[3];
        var read = stream.Read(preamble);
        if (read != 3 || preamble[0] != 0xef || preamble[1] != 0xbb || preamble[2] != 0xbf)
            stream.Position = 0;
        return stream;
    }

    private static FileStream OpenRange(string path, long startOffset, long endOffset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(startOffset);
        if (endOffset <= startOffset)
            throw new ArgumentOutOfRangeException(nameof(endOffset));

        var stream = Open(path);
        if (endOffset > stream.Length)
        {
            stream.Dispose();
            throw new ArgumentOutOfRangeException(nameof(endOffset));
        }

        stream.Position = startOffset;
        return stream;
    }

    private static FileStream Open(string path)
    {
        return new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1,
            FileOptions.SequentialScan);
    }

    private static bool IsWhitespace(byte value)
    {
        return value is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n';
    }
}
