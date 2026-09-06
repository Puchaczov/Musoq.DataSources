#nullable enable

using System;
using System.IO;
using System.Threading;

using Musoq.DataSources.Search.Components.Diagnostics;

namespace Musoq.DataSources.Search.Components.Bytes;

/// <summary>
///     Bounded raw-byte scanner used by the byte occurrence source.
/// </summary>
internal static class SearchByteScanner
{
    public const int DefaultBlockSize = 64 * 1024;

    public static void ScanFile(
        string filePath,
        SearchBytePattern pattern,
        bool materializeMatchedBytes,
        Action<long, byte[]?> match,
        Action? readerOpened,
        CancellationToken cancellationToken,
        int blockSize = DefaultBlockSize)
    {
        ArgumentNullException.ThrowIfNull(match);
        ScanFile(
            filePath,
            pattern,
            materializeMatchedBytes,
            materializeWindowBytes: false,
            (offset, matchedBytes, _) => match(offset, matchedBytes),
            readerOpened,
            cancellationToken,
            blockSize);
    }

    public static void ScanFile(
        string filePath,
        SearchBytePattern pattern,
        bool materializeMatchedBytes,
        bool materializeWindowBytes,
        Action<long, byte[]?, SearchByteWindow?> match,
        Action? readerOpened,
        CancellationToken cancellationToken,
        int blockSize = DefaultBlockSize)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(match);
        if (blockSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(blockSize));

        try
        {
            if (pattern.Window.IsEnabled)
            {
                ScanWithWindow(
                    filePath,
                    pattern,
                    materializeMatchedBytes,
                    materializeWindowBytes,
                    match,
                    readerOpened,
                    cancellationToken,
                    blockSize);
            }
            else
            {
                ScanWithoutWindow(
                    filePath,
                    pattern,
                    materializeMatchedBytes,
                    match,
                    readerOpened,
                    cancellationToken,
                    blockSize);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ISearchDiagnosticException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new SearchSourceReadException(
                SearchDiagnosticCatalog.SourceReadFailed(filePath),
                exception);
        }
    }

    private static void ScanWithoutWindow(
        string filePath,
        SearchBytePattern pattern,
        bool materializeMatchedBytes,
        Action<long, byte[]?, SearchByteWindow?> match,
        Action? readerOpened,
        CancellationToken cancellationToken,
        int blockSize)
    {
        var patternBytes = pattern.Bytes.Span;
        var masks = pattern.Masks.Span;
        var carryLength = pattern.Length - 1;
        var readBuffer = new byte[blockSize];
        var scanBuffer = new byte[checked(blockSize + carryLength)];
        var carry = new byte[carryLength];
        var carryCount = 0;
        var totalRead = 0L;
        var nextAllowedOffset = 0L;

        using var stream = Open(filePath, blockSize);
        readerOpened?.Invoke();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bytesRead = stream.Read(readBuffer, 0, readBuffer.Length);
            cancellationToken.ThrowIfCancellationRequested();
            if (bytesRead == 0)
                break;

            if (carryCount > 0)
                Buffer.BlockCopy(carry, 0, scanBuffer, 0, carryCount);
            Buffer.BlockCopy(readBuffer, 0, scanBuffer, carryCount, bytesRead);

            var combinedLength = checked(carryCount + bytesRead);
            var scanStartOffset = checked(totalRead - carryCount);
            var lastStart = combinedLength - pattern.Length;
            for (var position = 0; position <= lastStart; position++)
            {
                var absoluteOffset = checked(scanStartOffset + position);
                if (absoluteOffset < nextAllowedOffset ||
                    !Matches(scanBuffer.AsSpan(position, pattern.Length), patternBytes, masks))
                {
                    continue;
                }

                var matched = materializeMatchedBytes
                    ? scanBuffer.AsSpan(position, pattern.Length).ToArray()
                    : null;
                match(absoluteOffset, matched, null);
                nextAllowedOffset = checked(absoluteOffset + pattern.Length);
            }

            totalRead = checked(totalRead + bytesRead);
            carryCount = Math.Min(carryLength, combinedLength);
            if (carryCount > 0)
            {
                Buffer.BlockCopy(
                    scanBuffer,
                    combinedLength - carryCount,
                    carry,
                    0,
                    carryCount);
            }
        }
    }

    private static void ScanWithWindow(
        string filePath,
        SearchBytePattern pattern,
        bool materializeMatchedBytes,
        bool materializeWindowBytes,
        Action<long, byte[]?, SearchByteWindow?> match,
        Action? readerOpened,
        CancellationToken cancellationToken,
        int blockSize)
    {
        var patternBytes = pattern.Bytes;
        var masks = pattern.Masks;
        var window = pattern.Window;
        var readBuffer = new byte[blockSize];
        var rolling = new RollingByteBuffer(
            checked(blockSize + pattern.Length + window.BeforeBytes + window.AfterBytes));
        var totalRead = 0L;
        var scanOffset = 0L;
        var nextAllowedOffset = 0L;

        using var stream = Open(filePath, blockSize);
        readerOpened?.Invoke();

        void ProcessAvailable(bool endOfFile)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (scanOffset < nextAllowedOffset)
                    scanOffset = nextAllowedOffset;

                if (scanOffset > totalRead - pattern.Length)
                    break;

                if (!rolling.Matches(scanOffset, patternBytes.Span, masks.Span))
                {
                    scanOffset = checked(scanOffset + 1);
                    continue;
                }

                var requiredEnd = checked(
                    scanOffset + pattern.Length + window.AfterBytes);
                if (!endOfFile && requiredEnd > totalRead)
                    break;

                var start = Math.Max(0L, scanOffset - window.BeforeBytes);
                var end = Math.Min(requiredEnd, totalRead);
                var byteLength = checked(end - start);
                var matchedBytes = materializeMatchedBytes
                    ? rolling.Copy(scanOffset, pattern.Length)
                    : null;
                var windowBytes = materializeWindowBytes
                    ? rolling.Copy(start, checked((int)byteLength))
                    : null;
                var complete = scanOffset >= window.BeforeBytes &&
                               end == requiredEnd;
                var windowValue = new SearchByteWindow(
                    start,
                    byteLength,
                    complete,
                    windowBytes);

                match(scanOffset, matchedBytes, windowValue);
                nextAllowedOffset = checked(scanOffset + pattern.Length);
                scanOffset = nextAllowedOffset;
            }

            rolling.DiscardBefore(Math.Max(0L, scanOffset - window.BeforeBytes));
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bytesRead = stream.Read(readBuffer, 0, readBuffer.Length);
            cancellationToken.ThrowIfCancellationRequested();
            if (bytesRead == 0)
            {
                ProcessAvailable(endOfFile: true);
                break;
            }

            rolling.Append(readBuffer.AsSpan(0, bytesRead));
            totalRead = checked(totalRead + bytesRead);
            ProcessAvailable(endOfFile: false);
        }
    }

    private static FileStream Open(string filePath, int blockSize)
    {
        return new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            blockSize,
            FileOptions.SequentialScan);
    }

    private static bool Matches(
        ReadOnlySpan<byte> candidate,
        ReadOnlySpan<byte> pattern,
        ReadOnlySpan<byte> masks)
    {
        for (var index = 0; index < pattern.Length; index++)
        {
            if ((candidate[index] & masks[index]) !=
                (pattern[index] & masks[index]))
            {
                return false;
            }
        }

        return true;
    }

    private sealed class RollingByteBuffer
    {
        private byte[] _buffer;
        private long _startOffset;
        private int _count;

        public RollingByteBuffer(int initialCapacity)
        {
            if (initialCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));

            _buffer = new byte[initialCapacity];
        }

        private long EndOffset => checked(_startOffset + _count);

        public void Append(ReadOnlySpan<byte> bytes)
        {
            EnsureCapacity(checked(_count + bytes.Length));
            bytes.CopyTo(_buffer.AsSpan(_count));
            _count = checked(_count + bytes.Length);
        }

        public bool Matches(
            long offset,
            ReadOnlySpan<byte> pattern,
            ReadOnlySpan<byte> masks)
        {
            return SearchByteScanner.Matches(
                Slice(offset, pattern.Length),
                pattern,
                masks);
        }

        public byte[] Copy(long offset, int length)
        {
            return Slice(offset, length).ToArray();
        }

        public void DiscardBefore(long offset)
        {
            if (offset <= _startOffset)
                return;

            if (offset >= EndOffset)
            {
                _startOffset = EndOffset;
                _count = 0;
                return;
            }

            var discard = checked((int)(offset - _startOffset));
            Buffer.BlockCopy(_buffer, discard, _buffer, 0, _count - discard);
            _startOffset = offset;
            _count -= discard;
        }

        private ReadOnlySpan<byte> Slice(long offset, int length)
        {
            if (offset < _startOffset ||
                offset > EndOffset ||
                length < 0 ||
                length > EndOffset - offset)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            return _buffer.AsSpan(checked((int)(offset - _startOffset)), length);
        }

        private void EnsureCapacity(int required)
        {
            if (required <= _buffer.Length)
                return;

            var capacity = Math.Max(required, checked(_buffer.Length * 2));
            Array.Resize(ref _buffer, capacity);
        }
    }
}
