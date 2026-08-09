#nullable enable

using System;
using System.Buffers;
using System.IO;
using System.Text;
using System.Threading;

namespace Musoq.DataSources.SeparatedValues;

internal sealed class SeparatedValuesUtf8Reader : IDisposable
{
    private const int SequentialInitialBufferSize = 1024 * 1024;
    private const int PartitionInitialBufferSize = 256 * 1024;
    private const int MaximumBufferSize = 256 * 1024 * 1024;
    private static readonly byte[] Utf8Bom = [0xef, 0xbb, 0xbf];

    private readonly CancellationToken _cancellationToken;
    private readonly long _rangeEndOffset;
    private readonly FileStream _stream;
    private readonly byte _separator;
    private readonly int _skipLines;
    private byte[] _buffer;
    private int _recordStart;
    private int _scanOffset;
    private int _bufferedLength;
    private long _bufferStartOffset;
    private ScanState _state;
    private bool _endOfFile;
    private bool _initialized;
    private bool _disposed;

    public SeparatedValuesUtf8Reader(
        string path,
        byte separator,
        int skipLines = 0,
        CancellationToken cancellationToken = default)
        : this(path, separator, skipLines, SequentialInitialBufferSize, cancellationToken)
    {
    }

    public SeparatedValuesUtf8Reader(
        string path,
        byte separator,
        int skipLines,
        int initialBufferSize,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfNegative(skipLines);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(initialBufferSize);
        if (initialBufferSize > MaximumBufferSize)
            throw new ArgumentOutOfRangeException(nameof(initialBufferSize));
        if (separator is (byte)'"' or (byte)'\r' or (byte)'\n')
            throw new ArgumentOutOfRangeException(nameof(separator));

        _stream = new FileStream(
            Path.GetFullPath(path),
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1,
            FileOptions.SequentialScan);
        _separator = separator;
        _skipLines = skipLines;
        _cancellationToken = cancellationToken;
        _rangeEndOffset = _stream.Length;
        _buffer = ArrayPool<byte>.Shared.Rent(initialBufferSize);
    }

    public SeparatedValuesUtf8Reader(
        string path,
        byte separator,
        long startOffset,
        long endOffset,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfNegative(startOffset);
        if (endOffset <= startOffset)
            throw new ArgumentOutOfRangeException(nameof(endOffset));
        if (separator is (byte)'"' or (byte)'\r' or (byte)'\n')
            throw new ArgumentOutOfRangeException(nameof(separator));

        _stream = new FileStream(
            Path.GetFullPath(path),
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1,
            FileOptions.SequentialScan);
        if (endOffset > _stream.Length)
            throw new ArgumentOutOfRangeException(nameof(endOffset));

        _stream.Position = startOffset;
        _separator = separator;
        _skipLines = 0;
        _cancellationToken = cancellationToken;
        _rangeEndOffset = endOffset;
        _buffer = ArrayPool<byte>.Shared.Rent(PartitionInitialBufferSize);
        _bufferStartOffset = startOffset;
        _initialized = true;
    }

    public bool TryRead(out SeparatedValuesUtf8Record record)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _cancellationToken.ThrowIfCancellationRequested();
        Initialize();

        while (true)
        {
            while (_scanOffset < _bufferedLength)
            {
                var value = _buffer[_scanOffset];

                switch (_state)
                {
                    case ScanState.FieldStart:
                        if (value == (byte)'"')
                        {
                            _state = ScanState.Quoted;
                            _scanOffset++;
                            continue;
                        }

                        if (value == _separator)
                        {
                            _scanOffset++;
                            continue;
                        }

                        if (value == (byte)'\n')
                        {
                            if (TryCompleteRecord(_scanOffset, 1, out record))
                                return true;
                            continue;
                        }

                        if (value == (byte)'\r')
                        {
                            if (!EnsureLineFeedAfterCarriageReturn())
                                throw InvalidData("A carriage return outside a quoted field must be followed by a line feed.");
                            if (TryCompleteRecord(_scanOffset, 2, out record))
                                return true;
                            continue;
                        }

                        _state = ScanState.Unquoted;
                        _scanOffset++;
                        continue;

                    case ScanState.Unquoted:
                        if (value == _separator)
                        {
                            _state = ScanState.FieldStart;
                            _scanOffset++;
                            continue;
                        }

                        if (value == (byte)'"')
                            throw InvalidData("A quote may only appear at the beginning of a field.");

                        if (value == (byte)'\n')
                        {
                            if (TryCompleteRecord(_scanOffset, 1, out record))
                                return true;
                            continue;
                        }

                        if (value == (byte)'\r')
                        {
                            if (!EnsureLineFeedAfterCarriageReturn())
                                throw InvalidData("A carriage return outside a quoted field must be followed by a line feed.");
                            if (TryCompleteRecord(_scanOffset, 2, out record))
                                return true;
                            continue;
                        }

                        _scanOffset++;
                        continue;

                    case ScanState.Quoted:
                        if (value == (byte)'"')
                            _state = ScanState.AfterQuote;
                        _scanOffset++;
                        continue;

                    case ScanState.AfterQuote:
                        if (value == (byte)'"')
                        {
                            _state = ScanState.Quoted;
                            _scanOffset++;
                            continue;
                        }

                        if (value == _separator)
                        {
                            _state = ScanState.FieldStart;
                            _scanOffset++;
                            continue;
                        }

                        if (value == (byte)'\n')
                        {
                            if (TryCompleteRecord(_scanOffset, 1, out record))
                                return true;
                            continue;
                        }

                        if (value == (byte)'\r')
                        {
                            if (!EnsureLineFeedAfterCarriageReturn())
                                throw InvalidData("A carriage return outside a quoted field must be followed by a line feed.");
                            if (TryCompleteRecord(_scanOffset, 2, out record))
                                return true;
                            continue;
                        }

                        throw InvalidData("Only a separator or record ending may follow a closing quote.");

                    default:
                        throw new InvalidOperationException($"Unsupported separated-values scanner state '{_state}'.");
                }
            }

            if (_endOfFile)
            {
                if (_state == ScanState.Quoted)
                    throw InvalidData("The final quoted field is not terminated.");

                if (_recordStart == _bufferedLength)
                {
                    record = default;
                    return false;
                }

                var bytes = _buffer.AsSpan(_recordStart, _bufferedLength - _recordStart);
                ValidateUtf8(bytes);
                record = new SeparatedValuesUtf8Record(
                    bytes,
                    _separator,
                    _bufferStartOffset + _recordStart,
                    _bufferStartOffset + _bufferedLength);
                _recordStart = _bufferedLength;
                _scanOffset = _bufferedLength;
                _state = ScanState.FieldStart;
                return true;
            }

            FillBuffer();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _stream.Dispose();
        ArrayPool<byte>.Shared.Return(_buffer);
        _buffer = [];
    }

    private void Initialize()
    {
        if (_initialized)
            return;

        _initialized = true;
        FillBuffer();
        while (_bufferedLength < Utf8Bom.Length && !_endOfFile)
            FillBuffer();

        if (_bufferedLength >= Utf8Bom.Length && _buffer.AsSpan(0, Utf8Bom.Length).SequenceEqual(Utf8Bom))
        {
            _recordStart = Utf8Bom.Length;
            _scanOffset = Utf8Bom.Length;
        }

        for (var line = 0; line < _skipLines && TrySkipPhysicalLine(); line++)
        {
        }
    }

    private bool TrySkipPhysicalLine()
    {
        while (true)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            var lineFeed = _buffer.AsSpan(_scanOffset, _bufferedLength - _scanOffset).IndexOf((byte)'\n');
            if (lineFeed >= 0)
            {
                var terminatorOffset = _scanOffset + lineFeed;
                var contentEnd = terminatorOffset;
                if (contentEnd > _recordStart && _buffer[contentEnd - 1] == (byte)'\r')
                    contentEnd--;

                ValidatePhysicalLine(_buffer.AsSpan(_recordStart, contentEnd - _recordStart));
                _scanOffset = terminatorOffset + 1;
                _recordStart = _scanOffset;
                _state = ScanState.FieldStart;
                return true;
            }

            _scanOffset = _bufferedLength;
            if (_endOfFile)
            {
                if (_recordStart == _bufferedLength)
                    return false;

                ValidatePhysicalLine(_buffer.AsSpan(_recordStart, _bufferedLength - _recordStart));
                _recordStart = _bufferedLength;
                _scanOffset = _bufferedLength;
                _state = ScanState.FieldStart;
                return true;
            }

            FillBuffer();
        }
    }

    private bool TryCompleteRecord(
        int terminatorOffset,
        int terminatorLength,
        out SeparatedValuesUtf8Record record)
    {
        var start = _recordStart;
        var length = terminatorOffset - start;
        var end = terminatorOffset + terminatorLength;
        _scanOffset = end;
        _recordStart = end;
        _state = ScanState.FieldStart;

        if (length == 0)
        {
            record = default;
            return false;
        }

        var bytes = _buffer.AsSpan(start, length);
        ValidateUtf8(bytes);
        record = new SeparatedValuesUtf8Record(
            bytes,
            _separator,
            _bufferStartOffset + start,
            _bufferStartOffset + end);
        return true;
    }

    private bool EnsureLineFeedAfterCarriageReturn()
    {
        if (_scanOffset + 1 == _bufferedLength && !_endOfFile)
            FillBuffer();

        return _scanOffset + 1 < _bufferedLength && _buffer[_scanOffset + 1] == (byte)'\n';
    }

    private void FillBuffer()
    {
        if (_endOfFile)
            return;

        _cancellationToken.ThrowIfCancellationRequested();

        if (_bufferedLength == _buffer.Length)
        {
            if (_recordStart != 0)
            {
                var consumed = _recordStart;
                var remaining = _bufferedLength - consumed;
                _buffer.AsSpan(consumed, remaining).CopyTo(_buffer);
                _recordStart = 0;
                _scanOffset -= consumed;
                _bufferedLength = remaining;
                _bufferStartOffset += consumed;
            }
            else
            {
                if (_buffer.Length >= MaximumBufferSize)
                    throw InvalidData($"A separated-values record exceeds the {MaximumBufferSize:N0}-byte safety limit.");

                var replacement = ArrayPool<byte>.Shared.Rent(Math.Min(_buffer.Length * 2, MaximumBufferSize));
                _buffer.AsSpan(0, _bufferedLength).CopyTo(replacement);
                ArrayPool<byte>.Shared.Return(_buffer);
                _buffer = replacement;
            }
        }

        var absoluteBufferedEnd = _bufferStartOffset + _bufferedLength;
        if (absoluteBufferedEnd >= _rangeEndOffset)
        {
            _endOfFile = true;
            return;
        }

        var capacity = _buffer.Length - _bufferedLength;
        var remainingBytes = _rangeEndOffset - absoluteBufferedEnd;
        var requested = (int)Math.Min(capacity, remainingBytes);
        var read = _stream.Read(_buffer, _bufferedLength, requested);
        if (read == 0)
        {
            _endOfFile = true;
            return;
        }

        _bufferedLength += read;
        if (absoluteBufferedEnd + read >= _rangeEndOffset)
            _endOfFile = true;
    }

    private InvalidDataException InvalidData(string message)
    {
        return new InvalidDataException($"{message} Byte offset: {_bufferStartOffset + _scanOffset:N0}.");
    }

    private static void ValidatePhysicalLine(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IndexOf((byte)'\r') >= 0)
            throw new InvalidDataException("A physical preamble line contains a bare carriage return.");
        ValidateUtf8(bytes);
    }

    private static void ValidateUtf8(ReadOnlySpan<byte> bytes)
    {
        while (!bytes.IsEmpty)
        {
            var nonAscii = bytes.IndexOfAnyInRange((byte)0x80, byte.MaxValue);
            if (nonAscii < 0)
                return;

            bytes = bytes[nonAscii..];
            var status = Rune.DecodeFromUtf8(bytes, out _, out var consumed);
            if (status != OperationStatus.Done)
                throw new InvalidDataException("The separated-values source is not valid UTF-8.");
            bytes = bytes[consumed..];
        }
    }

    private enum ScanState : byte
    {
        FieldStart,
        Unquoted,
        Quoted,
        AfterQuote
    }
}

internal readonly ref struct SeparatedValuesUtf8Record
{
    private readonly ReadOnlySpan<byte> _bytes;
    private readonly byte _separator;

    public SeparatedValuesUtf8Record(
        ReadOnlySpan<byte> bytes,
        byte separator,
        long startOffset,
        long endOffset)
    {
        _bytes = bytes;
        _separator = separator;
        StartOffset = startOffset;
        EndOffset = endOffset;
    }

    public long StartOffset { get; }

    public long EndOffset { get; }

    public SeparatedValuesUtf8FieldEnumerator GetEnumerator()
    {
        return new SeparatedValuesUtf8FieldEnumerator(_bytes, _separator);
    }
}

internal ref struct SeparatedValuesUtf8FieldEnumerator
{
    private readonly ReadOnlySpan<byte> _record;
    private readonly byte _separator;
    private int _nextOffset;
    private bool _finished;

    public SeparatedValuesUtf8FieldEnumerator(ReadOnlySpan<byte> record, byte separator)
    {
        _record = record;
        _separator = separator;
        _nextOffset = 0;
        _finished = false;
        Current = default;
    }

    public SeparatedValuesUtf8Field Current { get; private set; }

    public bool MoveNext()
    {
        if (_finished)
            return false;

        var start = _nextOffset;
        if (start < _record.Length && _record[start] == (byte)'"')
        {
            var valueStart = start + 1;
            var offset = valueStart;
            var needsUnescaping = false;

            while (offset < _record.Length)
            {
                if (_record[offset] != (byte)'"')
                {
                    offset++;
                    continue;
                }

                if (offset + 1 < _record.Length && _record[offset + 1] == (byte)'"')
                {
                    needsUnescaping = true;
                    offset += 2;
                    continue;
                }

                Current = new SeparatedValuesUtf8Field(_record[valueStart..offset], true, needsUnescaping);
                SetNextOffset(offset + 1);
                return true;
            }

            throw new InvalidDataException("The quoted field is not terminated.");
        }

        var end = start;
        while (end < _record.Length && _record[end] != _separator)
            end++;

        Current = new SeparatedValuesUtf8Field(_record[start..end], false, false);
        SetNextOffset(end);
        return true;
    }

    private void SetNextOffset(int endOffset)
    {
        if (endOffset == _record.Length)
        {
            _finished = true;
            return;
        }

        if (_record[endOffset] != _separator)
            throw new InvalidDataException("A field was not followed by the configured separator.");

        _nextOffset = endOffset + 1;
    }
}

internal readonly ref struct SeparatedValuesUtf8Field
{
    public SeparatedValuesUtf8Field(
        ReadOnlySpan<byte> encodedValue,
        bool wasQuoted,
        bool needsUnescaping)
    {
        EncodedValue = encodedValue;
        WasQuoted = wasQuoted;
        NeedsUnescaping = needsUnescaping;
    }

    public ReadOnlySpan<byte> EncodedValue { get; }

    public bool WasQuoted { get; }

    public bool NeedsUnescaping { get; }

    public string Decode()
    {
        if (!NeedsUnescaping)
            return Encoding.UTF8.GetString(EncodedValue);

        var destination = GC.AllocateUninitializedArray<byte>(EncodedValue.Length);
        var written = 0;

        for (var offset = 0; offset < EncodedValue.Length; offset++)
        {
            var value = EncodedValue[offset];
            destination[written++] = value;

            if (value == (byte)'"' && offset + 1 < EncodedValue.Length && EncodedValue[offset + 1] == (byte)'"')
                offset++;
        }

        return Encoding.UTF8.GetString(destination.AsSpan(0, written));
    }

    public bool ValueEquals(ReadOnlySpan<byte> expected)
    {
        if (!NeedsUnescaping)
            return EncodedValue.SequenceEqual(expected);

        var expectedOffset = 0;
        for (var offset = 0; offset < EncodedValue.Length; offset++)
        {
            if (expectedOffset == expected.Length || EncodedValue[offset] != expected[expectedOffset])
                return false;

            expectedOffset++;
            if (EncodedValue[offset] == (byte)'"' &&
                offset + 1 < EncodedValue.Length && EncodedValue[offset + 1] == (byte)'"')
                offset++;
        }

        return expectedOffset == expected.Length;
    }
}
