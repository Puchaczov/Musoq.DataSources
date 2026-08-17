#nullable enable

using System;
using System.Buffers;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace Musoq.DataSources.SeparatedValues;

internal interface ISeparatedValuesByteBlockSourceFactory
{
    ISeparatedValuesByteBlockSource Open(string path, long expectedLength);
}

internal interface ISeparatedValuesByteBlockSource : IDisposable
{
    ValueTask<SeparatedValuesByteBlock> ReadAsync(
        long sequence,
        long offset,
        int count,
        CancellationToken cancellationToken);
}

internal interface ISeparatedValuesRecordBoundaryAnalyzer
{
    SeparatedValuesBlockAnalysis Analyze(SeparatedValuesByteBlock block);

    SeparatedValuesBlockAnalysis Analyze(
        SeparatedValuesByteBlock block,
        SeparatedValuesDialect dialect)
    {
        return Analyze(block);
    }

    SeparatedValuesBlockAnalysis AnalyzeFraming(
        SeparatedValuesByteBlock block,
        SeparatedValuesFramingAnalysisOptions options)
    {
        return Analyze(block);
    }

    SeparatedValuesBlockAnalysis AnalyzeFraming(
        SeparatedValuesByteBlock block,
        SeparatedValuesFramingAnalysisOptions options,
        SeparatedValuesDialect dialect)
    {
        return AnalyzeFraming(block, options);
    }
}

internal readonly record struct SeparatedValuesFramingAnalysisOptions(
    byte Separator,
    int ExpectedWidth);

internal enum SeparatedValuesCompactValidationError : byte
{
    None,
    BareCarriageReturn,
    ExcessColumns
}

internal sealed class RandomAccessSeparatedValuesByteBlockSourceFactory : ISeparatedValuesByteBlockSourceFactory
{
    public ISeparatedValuesByteBlockSource Open(string path, long expectedLength)
    {
        return new RandomAccessSeparatedValuesByteBlockSource(path, expectedLength);
    }
}

internal sealed class RandomAccessSeparatedValuesByteBlockSource : ISeparatedValuesByteBlockSource
{
    private readonly SafeFileHandle _handle;

    public RandomAccessSeparatedValuesByteBlockSource(string path, long expectedLength)
    {
        _handle = File.OpenHandle(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            FileOptions.Asynchronous | FileOptions.RandomAccess);
        if (RandomAccess.GetLength(_handle) != expectedLength)
        {
            _handle.Dispose();
            throw new IOException($"Separated-values source '{path}' changed length after planning.");
        }
    }

    public async ValueTask<SeparatedValuesByteBlock> ReadAsync(
        long sequence,
        long offset,
        int count,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sequence);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        var buffer = ArrayPool<byte>.Shared.Rent(count);
        SeparatedValuesStructuralMemoryBudget.Lease? memoryLease = null;
        var total = 0;
        try
        {
            memoryLease = await SeparatedValuesStructuralMemoryBudget.AcquireAsync(
                    buffer.Length,
                    cancellationToken)
                .ConfigureAwait(false);
            while (total < count)
            {
                var read = await RandomAccess.ReadAsync(
                        _handle,
                        buffer.AsMemory(total, count - total),
                        offset + total,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                    break;
                total += read;
            }

            return new SeparatedValuesByteBlock(sequence, offset, buffer, total, memoryLease);
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(buffer);
            memoryLease?.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        _handle.Dispose();
    }
}

internal sealed class SeparatedValuesByteBlock : IDisposable
{
    private byte[]? _buffer;
    private SeparatedValuesStructuralMemoryBudget.Lease? _memoryLease;

    public SeparatedValuesByteBlock(
        long sequence,
        long offset,
        byte[] buffer,
        int length,
        SeparatedValuesStructuralMemoryBudget.Lease? memoryLease = null)
    {
        Sequence = sequence;
        Offset = offset;
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        _memoryLease = memoryLease;
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        if (length > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(length));
        Length = length;
    }

    public long Sequence { get; }

    public long Offset { get; }

    public int Length { get; }

    public byte[] Buffer => _buffer ?? throw new ObjectDisposedException(nameof(SeparatedValuesByteBlock));

    public ReadOnlySpan<byte> Span => Buffer.AsSpan(0, Length);

    public void Dispose()
    {
        var buffer = Interlocked.Exchange(ref _buffer, null);
        if (buffer is not null)
            ArrayPool<byte>.Shared.Return(buffer);
        Interlocked.Exchange(ref _memoryLease, null)?.Dispose();
    }
}

internal sealed class SeparatedValuesBlockAnalysis : IDisposable
{
    private readonly bool _compact;
    private int[]? _newlines;

    public SeparatedValuesBlockAnalysis(
        SeparatedValuesByteBlock block,
        int[] newlines,
        int newlineCount,
        bool quoteParity)
    {
        Block = block;
        _newlines = newlines;
        NewlineCount = newlineCount;
        QuoteParity = quoteParity;
    }

    private SeparatedValuesBlockAnalysis(
        SeparatedValuesByteBlock block,
        int newlineCount,
        int firstBoundary,
        int lastBoundary,
        long tailRowCount,
        int firstTailRecordOffset,
        int lastTailRecordEndOffset,
        bool tailIsAscii,
        SeparatedValuesCompactValidationError validationError,
        int validationErrorOffset,
        long validationErrorTailRow)
    {
        Block = block;
        NewlineCount = newlineCount;
        FirstBoundary = firstBoundary;
        LastBoundary = lastBoundary;
        TailRowCount = tailRowCount;
        FirstTailRecordOffset = firstTailRecordOffset;
        LastTailRecordEndOffset = lastTailRecordEndOffset;
        TailIsAscii = tailIsAscii;
        ValidationError = validationError;
        ValidationErrorOffset = validationErrorOffset;
        ValidationErrorTailRow = validationErrorTailRow;
        _compact = true;
    }

    public SeparatedValuesByteBlock Block { get; }

    public int NewlineCount { get; private set; }

    public bool QuoteParity { get; }

    public bool IsCompact => _compact;

    public int FirstBoundary { get; }

    public int LastBoundary { get; }

    public long TailRowCount { get; }

    public int FirstTailRecordOffset { get; }

    public int LastTailRecordEndOffset { get; }

    public bool TailIsAscii { get; }

    public SeparatedValuesCompactValidationError ValidationError { get; }

    public int ValidationErrorOffset { get; }

    public long ValidationErrorTailRow { get; }

    public ReadOnlySpan<int> Newlines => (_newlines ?? throw new ObjectDisposedException(nameof(SeparatedValuesBlockAnalysis)))
        .AsSpan(0, NewlineCount);

    public void SelectRecordBoundaries(bool incomingQuoted)
    {
        if (_compact)
        {
            if (incomingQuoted)
                NewlineCount = 0;
            return;
        }

        var newlines = _newlines ?? throw new ObjectDisposedException(nameof(SeparatedValuesBlockAnalysis));
        var output = 0;
        for (var index = 0; index < NewlineCount; index++)
        {
            var encoded = newlines[index];
            var localQuoted = encoded < 0;
            if (localQuoted != incomingQuoted)
                continue;
            newlines[output++] = localQuoted ? ~encoded : encoded;
        }

        NewlineCount = output;
    }

    public int[]? DetachNewlines()
    {
        if (_compact)
            return null;
        var newlines = Interlocked.Exchange(ref _newlines, null)
                       ?? throw new ObjectDisposedException(nameof(SeparatedValuesBlockAnalysis));
        return newlines;
    }

    public static SeparatedValuesBlockAnalysis CompactUnquoted(
        SeparatedValuesByteBlock block,
        int newlineCount,
        int firstBoundary,
        int lastBoundary,
        long tailRowCount,
        int firstTailRecordOffset,
        int lastTailRecordEndOffset,
        bool tailIsAscii,
        SeparatedValuesCompactValidationError validationError,
        int validationErrorOffset,
        long validationErrorTailRow)
    {
        return new SeparatedValuesBlockAnalysis(
            block,
            newlineCount,
            firstBoundary,
            lastBoundary,
            tailRowCount,
            firstTailRecordOffset,
            lastTailRecordEndOffset,
            tailIsAscii,
            validationError,
            validationErrorOffset,
            validationErrorTailRow);
    }

    public void Dispose()
    {
        var newlines = Interlocked.Exchange(ref _newlines, null);
        if (newlines is not null)
            ArrayPool<int>.Shared.Return(newlines);
    }
}

internal sealed class QuoteParitySeparatedValuesRecordBoundaryAnalyzer : ISeparatedValuesRecordBoundaryAnalyzer
{
    public SeparatedValuesBlockAnalysis Analyze(
        SeparatedValuesByteBlock block,
        SeparatedValuesDialect dialect)
    {
        ArgumentNullException.ThrowIfNull(dialect);
        if (dialect.Quote == (byte)'\"' &&
            dialect.EscapeMode == SeparatedValuesEscapeMode.Double &&
            dialect.RecordEndingMode == SeparatedValuesRecordEndingMode.LfCrLf)
            return Analyze(block);

        return AnalyzeGeneral(block, dialect);
    }

    public SeparatedValuesBlockAnalysis AnalyzeFraming(
        SeparatedValuesByteBlock block,
        SeparatedValuesFramingAnalysisOptions options,
        SeparatedValuesDialect dialect)
    {
        return dialect.Quote == (byte)'\"' &&
               dialect.EscapeMode == SeparatedValuesEscapeMode.Double &&
               dialect.RecordEndingMode == SeparatedValuesRecordEndingMode.LfCrLf
            ? AnalyzeFraming(block, options)
            : Analyze(block, dialect);
    }

    public SeparatedValuesBlockAnalysis AnalyzeFraming(
        SeparatedValuesByteBlock block,
        SeparatedValuesFramingAnalysisOptions options)
    {
        var bytes = block.Span;
        // Quoted and CRLF blocks retain the general conditional-boundary representation.
        // Quote-free LF input can keep only counts and edge offsets, which avoids an int per row.
        if (bytes.IndexOfAny((byte)'"', (byte)'\r') < 0)
            return AnalyzeSimpleFraming(block, options);

        var newlineCount = 0;
        var firstBoundary = -1;
        var lastBoundary = -1;
        var tailRowCount = 0L;
        var firstTailRecordOffset = 0;
        var lastTailRecordEndOffset = 0;
        var recordStart = 0;
        var fields = 1;
        var currentRecordIsAscii = true;
        var tailIsAscii = true;
        var validationError = SeparatedValuesCompactValidationError.None;
        var validationErrorOffset = -1;
        var validationErrorTailRow = 0L;

        for (var offset = 0; offset < bytes.Length; offset++)
        {
            var value = bytes[offset];
            if (value == (byte)'"')
                return Analyze(block);
            if (value != (byte)'\n')
            {
                if (newlineCount > 0)
                {
                    if (value == options.Separator)
                        fields++;
                    if (value >= 0x80)
                        currentRecordIsAscii = false;
                    if (value == (byte)'\r' &&
                        offset + 1 < bytes.Length &&
                        bytes[offset + 1] != (byte)'\n' &&
                        validationError == SeparatedValuesCompactValidationError.None)
                    {
                        validationError = SeparatedValuesCompactValidationError.BareCarriageReturn;
                        validationErrorOffset = offset;
                        validationErrorTailRow = tailRowCount + 1;
                    }
                }
                continue;
            }

            if (newlineCount == 0)
            {
                firstBoundary = offset;
            }
            else
            {
                var recordEnd = offset > recordStart && bytes[offset - 1] == (byte)'\r'
                    ? offset - 1
                    : offset;
                if (recordEnd > recordStart)
                {
                    if (fields > options.ExpectedWidth &&
                        validationError == SeparatedValuesCompactValidationError.None)
                    {
                        validationError = SeparatedValuesCompactValidationError.ExcessColumns;
                        validationErrorOffset = offset;
                        validationErrorTailRow = tailRowCount + 1;
                    }
                    if (tailRowCount == 0)
                        firstTailRecordOffset = recordStart;
                    lastTailRecordEndOffset = offset + 1;
                    tailRowCount++;
                    tailIsAscii &= currentRecordIsAscii;
                }
            }

            newlineCount++;
            lastBoundary = offset;
            recordStart = offset + 1;
            fields = 1;
            currentRecordIsAscii = true;
        }

        return SeparatedValuesBlockAnalysis.CompactUnquoted(
            block,
            newlineCount,
            firstBoundary,
            lastBoundary,
            tailRowCount,
            firstTailRecordOffset,
            lastTailRecordEndOffset,
            tailIsAscii,
            validationError,
            validationErrorOffset,
            validationErrorTailRow);
    }

    private static SeparatedValuesBlockAnalysis AnalyzeSimpleFraming(
        SeparatedValuesByteBlock block,
        SeparatedValuesFramingAnalysisOptions options)
    {
        return options.ExpectedWidth == 2 &&
               Avx2.IsSupported &&
               Bmi2.IsSupported &&
               block.Length >= Vector256<byte>.Count
            ? AnalyzeTwoColumnFraming(block, options)
            : AnalyzeSimpleFramingScalar(block, options);
    }

    private static SeparatedValuesBlockAnalysis AnalyzeTwoColumnFraming(
        SeparatedValuesByteBlock block,
        SeparatedValuesFramingAnalysisOptions options)
    {
        var bytes = block.Span;
        var firstBoundary = -1;
        var lastBoundary = -1;
        var previousNewlineOffset = -1;
        var firstTailRecordOffset = 0;
        var lastTailRecordEndOffset = 0;
        var tailRowCount = 0L;
        var totalNewlineCount = 0;
        var tailStarted = false;
        var previousEventWasSeparator = false;
        var previousByteWasNewline = false;
        var tailIsAscii = true;
        var excessColumns = false;
        var vectorWidth = Vector256<byte>.Count;
        var vectorEnd = bytes.Length - bytes.Length % vectorWidth;
        ref var first = ref MemoryMarshal.GetReference(bytes);
        var separator = Vector256.Create(options.Separator);
        var newline = Vector256.Create((byte)'\n');

        for (var offset = 0; offset < vectorEnd; offset += vectorWidth)
        {
            // Two-column count workloads dominate the large TABLE-contract path. Masks count
            // separators/newlines without revisiting every byte or allocating boundary arrays.
            var vector = Vector256.LoadUnsafe(ref first, (nuint)offset);
            var separatorMask = (uint)Avx2.MoveMask(Avx2.CompareEqual(vector, separator).AsSByte());
            var newlineMask = (uint)Avx2.MoveMask(Avx2.CompareEqual(vector, newline).AsSByte());
            if (Avx2.MoveMask(vector.AsSByte()) != 0)
                tailIsAscii = false;
            ProcessMasks(newlineMask, separatorMask, vectorWidth, offset);
        }

        if (vectorEnd < bytes.Length)
        {
            uint separatorMask = 0;
            uint newlineMask = 0;
            var width = bytes.Length - vectorEnd;
            for (var index = 0; index < width; index++)
            {
                var value = bytes[vectorEnd + index];
                if (value == options.Separator)
                    separatorMask |= 1u << index;
                else if (value == (byte)'\n')
                    newlineMask |= 1u << index;
                if (value >= 0x80)
                    tailIsAscii = false;
            }

            ProcessMasks(newlineMask, separatorMask, width, vectorEnd);
        }

        if (excessColumns)
            return AnalyzeSimpleFramingScalar(block, options);

        return SeparatedValuesBlockAnalysis.CompactUnquoted(
            block,
            totalNewlineCount,
            firstBoundary,
            lastBoundary,
            tailRowCount,
            firstTailRecordOffset,
            lastTailRecordEndOffset,
            tailIsAscii,
            SeparatedValuesCompactValidationError.None,
            -1,
            0);

        void ProcessMasks(uint newlineMask, uint separatorMask, int width, int absoluteOffset)
        {
            var originalNewlineMask = newlineMask;
            var newlineCountInLane = BitOperations.PopCount(originalNewlineMask);
            totalNewlineCount += newlineCountInLane;
            if (newlineCountInLane > 0)
            {
                var blankMask = originalNewlineMask & (originalNewlineMask << 1);
                if (previousByteWasNewline && (originalNewlineMask & 1) != 0)
                    blankMask |= 1;
                var nonBlankMask = originalNewlineMask & ~blankMask;

                if (firstBoundary < 0)
                {
                    var firstBit = BitOperations.TrailingZeroCount(originalNewlineMask);
                    firstBoundary = absoluteOffset + firstBit;
                    nonBlankMask &= ~(1u << firstBit);
                }

                if (nonBlankMask != 0)
                {
                    var firstBit = BitOperations.TrailingZeroCount(nonBlankMask);
                    if (tailRowCount == 0)
                    {
                        var lowerNewlines = originalNewlineMask & ((1u << firstBit) - 1);
                        var preceding = lowerNewlines != 0
                            ? absoluteOffset + 31 - BitOperations.LeadingZeroCount(lowerNewlines)
                            : previousNewlineOffset;
                        firstTailRecordOffset = preceding + 1;
                    }

                    var lastBit = 31 - BitOperations.LeadingZeroCount(nonBlankMask);
                    lastTailRecordEndOffset = absoluteOffset + lastBit + 1;
                    tailRowCount += BitOperations.PopCount(nonBlankMask);
                }

                var lastNewlineBit = 31 - BitOperations.LeadingZeroCount(originalNewlineMask);
                previousNewlineOffset = absoluteOffset + lastNewlineBit;
                lastBoundary = previousNewlineOffset;
            }

            var events = separatorMask | originalNewlineMask;
            if (!tailStarted && originalNewlineMask != 0)
            {
                var firstNewlineBit = BitOperations.TrailingZeroCount(originalNewlineMask);
                var throughFirstNewline = firstNewlineBit == 31
                    ? uint.MaxValue
                    : (1u << (firstNewlineBit + 1)) - 1;
                events &= ~throughFirstNewline;
                tailStarted = true;
                previousEventWasSeparator = false;
            }

            if (tailStarted && events != 0)
            {
                // PEXT compresses the ordered separator/newline events. Adjacent separator bits
                // mean width drift; the scalar kernel is then replayed only to produce diagnostics.
                var sequence = Bmi2.ParallelBitExtract(separatorMask, events);
                var eventCount = BitOperations.PopCount(events);
                excessColumns |= previousEventWasSeparator && (sequence & 1) != 0;
                excessColumns |= (sequence & (sequence >> 1)) != 0;
                previousEventWasSeparator = ((sequence >> (eventCount - 1)) & 1) != 0;
            }

            previousByteWasNewline = (originalNewlineMask & (1u << (width - 1))) != 0;
        }
    }

    private static SeparatedValuesBlockAnalysis AnalyzeSimpleFramingScalar(
        SeparatedValuesByteBlock block,
        SeparatedValuesFramingAnalysisOptions options)
    {
        var bytes = block.Span;
        var newlineCount = 0;
        var firstBoundary = -1;
        var lastBoundary = -1;
        var tailRowCount = 0L;
        var firstTailRecordOffset = 0;
        var lastTailRecordEndOffset = 0;
        var recordStart = 0;
        var fields = 1;
        var validationError = SeparatedValuesCompactValidationError.None;
        var validationErrorOffset = -1;
        var validationErrorTailRow = 0L;
        var cursor = 0;

        while (cursor < bytes.Length)
        {
            var relative = bytes[cursor..].IndexOfAny(options.Separator, (byte)'\n');
            if (relative < 0)
                break;
            var offset = cursor + relative;
            if (bytes[offset] == options.Separator)
            {
                if (newlineCount > 0)
                    fields++;
                cursor = offset + 1;
                continue;
            }

            if (newlineCount == 0)
            {
                firstBoundary = offset;
            }
            else if (offset > recordStart)
            {
                if (fields > options.ExpectedWidth &&
                    validationError == SeparatedValuesCompactValidationError.None)
                {
                    validationError = SeparatedValuesCompactValidationError.ExcessColumns;
                    validationErrorOffset = offset;
                    validationErrorTailRow = tailRowCount + 1;
                }
                if (tailRowCount == 0)
                    firstTailRecordOffset = recordStart;
                lastTailRecordEndOffset = offset + 1;
                tailRowCount++;
            }

            newlineCount++;
            lastBoundary = offset;
            recordStart = offset + 1;
            fields = 1;
            cursor = offset + 1;
        }

        var tailIsAscii = bytes.IndexOfAnyInRange((byte)0x80, byte.MaxValue) < 0;
        return SeparatedValuesBlockAnalysis.CompactUnquoted(
            block,
            newlineCount,
            firstBoundary,
            lastBoundary,
            tailRowCount,
            firstTailRecordOffset,
            lastTailRecordEndOffset,
            tailIsAscii,
            validationError,
            validationErrorOffset,
            validationErrorTailRow);
    }

    public SeparatedValuesBlockAnalysis Analyze(SeparatedValuesByteBlock block)
    {
        var capacity = Math.Max(16, block.Length / 16 + 1);
        var newlines = ArrayPool<int>.Shared.Rent(capacity);
        var count = 0;
        var quoted = false;

        try
        {
            var bytes = block.Span;
            for (var offset = 0; offset < bytes.Length; offset++)
            {
                var value = bytes[offset];
                if (value == (byte)'"')
                {
                    quoted = !quoted;
                    continue;
                }

                if (value != (byte)'\n')
                    continue;

                if (count == newlines.Length)
                {
                    var replacement = ArrayPool<int>.Shared.Rent(checked(newlines.Length * 2));
                    newlines.AsSpan(0, count).CopyTo(replacement);
                    ArrayPool<int>.Shared.Return(newlines);
                    newlines = replacement;
                }

                newlines[count++] = quoted ? ~offset : offset;
            }

            return new SeparatedValuesBlockAnalysis(block, newlines, count, quoted);
        }
        catch
        {
            ArrayPool<int>.Shared.Return(newlines);
            throw;
        }
    }

    private static SeparatedValuesBlockAnalysis AnalyzeGeneral(
        SeparatedValuesByteBlock block,
        SeparatedValuesDialect dialect)
    {
        var newlines = ArrayPool<int>.Shared.Rent(Math.Max(16, block.Length / 16 + 1));
        var count = 0;
        var quoted = false;
        var escaped = false;
        try
        {
            var bytes = block.Span;
            for (var offset = 0; offset < bytes.Length; offset++)
            {
                var value = bytes[offset];
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (quoted && dialect.EscapeMode == SeparatedValuesEscapeMode.Backslash &&
                    value == (byte)'\\')
                {
                    escaped = true;
                    continue;
                }

                if (dialect.Quote.HasValue && value == dialect.Quote.Value)
                {
                    quoted = !quoted;
                    continue;
                }

                if (value != (byte)'\n')
                    continue;

                if (count == newlines.Length)
                {
                    var replacement = ArrayPool<int>.Shared.Rent(checked(newlines.Length * 2));
                    newlines.AsSpan(0, count).CopyTo(replacement);
                    ArrayPool<int>.Shared.Return(newlines);
                    newlines = replacement;
                }

                newlines[count++] = quoted ? ~offset : offset;
            }

            return new SeparatedValuesBlockAnalysis(block, newlines, count, quoted);
        }
        catch
        {
            ArrayPool<int>.Shared.Return(newlines);
            throw;
        }
    }
}
