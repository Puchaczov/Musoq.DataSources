#nullable enable

using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Musoq.DataSources.Structured;

namespace Musoq.DataSources.SeparatedValues;

internal sealed class SeparatedValuesUtf8Reader : IDisposable
{
    private const int SequentialInitialBufferSize = 1024 * 1024;
    private const int PartitionInitialBufferSize = 256 * 1024;
    private const int MaximumBufferSize = 256 * 1024 * 1024;
    private static readonly byte[] Utf8Bom = [0xef, 0xbb, 0xbf];

    private readonly CancellationToken _cancellationToken;
    private readonly long _deadlineTimestamp;
    private readonly long _maximumBytesRead;
    private readonly long _rangeEndOffset;
    private readonly FileStream _stream;
    private SeparatedValuesDialect _dialect;
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
    private bool _budgetExhausted;
    private bool _useStrictFramingFastPath = true;
    private int _skippedLineCount;
    private long _bytesRead;

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
        _dialect = SeparatedValuesDialect.Strict(separator);
        _separator = separator;
        _skipLines = skipLines;
        _cancellationToken = cancellationToken;
        _rangeEndOffset = _stream.Length;
        _buffer = ArrayPool<byte>.Shared.Rent(initialBufferSize);
        _maximumBytesRead = long.MaxValue;
        _deadlineTimestamp = long.MaxValue;
    }

    internal SeparatedValuesUtf8Reader(
        string path,
        byte separator,
        int skipLines,
        int initialBufferSize,
        long maximumBytesRead,
        long deadlineTimestamp,
        CancellationToken cancellationToken)
        : this(path, separator, skipLines, initialBufferSize, cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumBytesRead);
        _maximumBytesRead = maximumBytesRead;
        _deadlineTimestamp = deadlineTimestamp;
    }

    internal SeparatedValuesUtf8Reader(
        string path,
        SeparatedValuesDialect dialect,
        int skipLines,
        int initialBufferSize,
        CancellationToken cancellationToken)
        : this(
            path,
            dialect?.Separator ?? throw new ArgumentNullException(nameof(dialect)),
            skipLines,
            initialBufferSize,
            cancellationToken)
    {
        _dialect = dialect;
    }

    internal SeparatedValuesUtf8Reader(
        string path,
        SeparatedValuesDialect dialect,
        int skipLines,
        int initialBufferSize,
        CancellationToken cancellationToken,
        bool useStrictFramingFastPath)
        : this(path, dialect, skipLines, initialBufferSize, cancellationToken)
    {
        _useStrictFramingFastPath = useStrictFramingFastPath;
    }

    internal SeparatedValuesUtf8Reader(
        string path,
        SeparatedValuesDialect dialect,
        int skipLines,
        int initialBufferSize,
        long maximumBytesRead,
        long deadlineTimestamp,
        CancellationToken cancellationToken)
        : this(
            path,
            dialect?.Separator ?? throw new ArgumentNullException(nameof(dialect)),
            skipLines,
            initialBufferSize,
            maximumBytesRead,
            deadlineTimestamp,
            cancellationToken)
    {
        _dialect = dialect;
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
            throw new ArgumentOutOfRangeException(
                nameof(endOffset),
                endOffset,
                $"The requested range ends at {endOffset}, but the current file length is {_stream.Length}.");

        _stream.Position = startOffset;
        _dialect = SeparatedValuesDialect.Strict(separator);
        _separator = separator;
        _skipLines = 0;
        _cancellationToken = cancellationToken;
        _rangeEndOffset = endOffset;
        _buffer = ArrayPool<byte>.Shared.Rent(PartitionInitialBufferSize);
        _bufferStartOffset = startOffset;
        _initialized = true;
        _maximumBytesRead = long.MaxValue;
        _deadlineTimestamp = long.MaxValue;
    }

    internal SeparatedValuesUtf8Reader(
        string path,
        SeparatedValuesDialect dialect,
        long startOffset,
        long endOffset,
        CancellationToken cancellationToken = default)
        : this(
            path,
            dialect?.Separator ?? throw new ArgumentNullException(nameof(dialect)),
            startOffset,
            endOffset,
            cancellationToken)
    {
        _dialect = dialect;
    }

    internal SeparatedValuesUtf8Reader(
        string path,
        SeparatedValuesDialect dialect,
        long startOffset,
        long endOffset,
        CancellationToken cancellationToken,
        bool useStrictFramingFastPath)
        : this(path, dialect, startOffset, endOffset, cancellationToken)
    {
        _useStrictFramingFastPath = useStrictFramingFastPath;
    }

    public long BytesRead => _bytesRead;

    public bool BudgetExhausted => _budgetExhausted;

    public int SkippedLineCount => _skippedLineCount;

    public long NextRecordOffset
    {
        get
        {
            Initialize();
            return _bufferStartOffset + _recordStart;
        }
    }

    public void Prepare()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _cancellationToken.ThrowIfCancellationRequested();
        Initialize();
    }

    internal void EnsureBufferedFingerprintMatches(StructuredFileIdentity identity)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_bufferStartOffset != 0 || _rangeEndOffset != identity.Length)
        {
            throw new InvalidOperationException(
                "A structured-source fingerprint can only be validated from a full-file reader.");
        }

        Initialize();
        while (!_endOfFile && !_budgetExhausted)
            FillBuffer();
        if (_budgetExhausted || _bufferedLength != identity.Length)
        {
            throw new InvalidOperationException(
                "The full structured source did not fit in the fingerprint-validation buffer.");
        }

        var fingerprint = StructuredFileIdentity.ComputeFingerprint(_buffer.AsSpan(0, _bufferedLength));
        if (fingerprint != identity.Fingerprint)
        {
            throw new StructuredSchemaDriftException(
                identity.CanonicalPath,
                "the file identity changed after planning");
        }
    }

    public bool TryRead(out SeparatedValuesUtf8Record record)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _cancellationToken.ThrowIfCancellationRequested();
        Initialize();

        while (true)
        {
            if (_state == ScanState.FieldStart && _recordStart == _scanOffset && TrySkipComment())
                continue;

            if (_useStrictFramingFastPath &&
                _dialect.IsStrict &&
                _state is ScanState.FieldStart or ScanState.Unquoted)
            {
                var remaining = _buffer.AsSpan(_scanOffset, _bufferedLength - _scanOffset);
                var relativeSpecial = remaining.IndexOfAny((byte)'"', (byte)'\r', (byte)'\n');
                if (relativeSpecial < 0)
                {
                    _scanOffset = _bufferedLength;
                }
                else
                {
                    _scanOffset += relativeSpecial;
                    var special = _buffer[_scanOffset];
                    if (special == (byte)'"')
                    {
                        if (_scanOffset != _recordStart && _buffer[_scanOffset - 1] != _separator)
                            throw InvalidData("A quote may only appear at the beginning of a field.");

                        _state = ScanState.Quoted;
                        _scanOffset++;
                        continue;
                    }

                    if (special == (byte)'\n')
                    {
                        if (TryCompleteRecord(_scanOffset, 1, out record))
                            return true;
                        continue;
                    }

                    var terminatorLength = GetRecordEndingLength();
                    if (terminatorLength == 0)
                        throw InvalidData("A carriage return outside a quoted field must be followed by a line feed.");
                    if (TryCompleteRecord(_scanOffset, terminatorLength, out record))
                        return true;
                    continue;
                }
            }

            while (_scanOffset < _bufferedLength)
            {
                var value = _buffer[_scanOffset];

                switch (_state)
                {
                    case ScanState.FieldStart:
                        if (_dialect.WhitespaceMode == SeparatedValuesWhitespaceMode.Trim &&
                            value is (byte)' ' or (byte)'\t')
                        {
                            _scanOffset++;
                            continue;
                        }

                        if (_dialect.Quote.HasValue && value == _dialect.Quote.Value)
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
                            var terminatorLength = GetRecordEndingLength();
                            if (terminatorLength == 0)
                                throw InvalidData("A carriage return outside a quoted field must be followed by a line feed.");
                            if (TryCompleteRecord(_scanOffset, terminatorLength, out record))
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

                        if (_dialect.Quote.HasValue && value == _dialect.Quote.Value)
                            throw InvalidData("A quote may only appear at the beginning of a field.");

                        if (value == (byte)'\n')
                        {
                            if (TryCompleteRecord(_scanOffset, 1, out record))
                                return true;
                            continue;
                        }

                        if (value == (byte)'\r')
                        {
                            var terminatorLength = GetRecordEndingLength();
                            if (terminatorLength == 0)
                                throw InvalidData("A carriage return outside a quoted field must be followed by a line feed.");
                            if (TryCompleteRecord(_scanOffset, terminatorLength, out record))
                                return true;
                            continue;
                        }

                        _scanOffset++;
                        continue;

                    case ScanState.Quoted:
                        if (_dialect.EscapeMode == SeparatedValuesEscapeMode.Backslash &&
                            value == (byte)'\\')
                        {
                            _state = ScanState.EscapedQuoted;
                            _scanOffset++;
                            continue;
                        }

                        if (_dialect.Quote.HasValue && value == _dialect.Quote.Value)
                            _state = ScanState.AfterQuote;
                        _scanOffset++;
                        continue;

                    case ScanState.EscapedQuoted:
                        _scanOffset++;
                        _state = ScanState.Quoted;
                        continue;

                    case ScanState.AfterQuote:
                        if (_dialect.WhitespaceMode == SeparatedValuesWhitespaceMode.Trim &&
                            value is (byte)' ' or (byte)'\t')
                        {
                            _scanOffset++;
                            continue;
                        }

                        if (_dialect.EscapeMode == SeparatedValuesEscapeMode.Double &&
                            _dialect.Quote.HasValue &&
                            value == _dialect.Quote.Value)
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
                            var terminatorLength = GetRecordEndingLength();
                            if (terminatorLength == 0)
                                throw InvalidData("A carriage return outside a quoted field must be followed by a line feed.");
                            if (TryCompleteRecord(_scanOffset, terminatorLength, out record))
                                return true;
                            continue;
                        }

                        throw InvalidData("Only a separator or record ending may follow a closing quote.");

                    default:
                        throw new InvalidOperationException($"Unsupported separated-values scanner state '{_state}'.");
                }
            }

            if (_budgetExhausted)
            {
                record = default;
                return false;
            }

            if (_endOfFile)
            {
                if (_state is ScanState.Quoted or ScanState.EscapedQuoted)
                    throw InvalidData("The final quoted field is not terminated.");

                if (_recordStart == _bufferedLength)
                {
                    record = default;
                    return false;
                }

                var bytes = _buffer.AsSpan(_recordStart, _bufferedLength - _recordStart);
                if (bytes.Length > _dialect.MaximumRecordBytes)
                    throw InvalidData($"A separated-values record exceeds {_dialect.MaximumRecordBytes:N0}-byte safety limit.");
                ValidateUtf8(bytes);
                record = new SeparatedValuesUtf8Record(
                    bytes,
                    _separator,
                    _bufferStartOffset + _recordStart,
                    _bufferStartOffset + _bufferedLength,
                    _dialect);
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
        while (_bufferedLength < Utf8Bom.Length && !_endOfFile && !_budgetExhausted)
            FillBuffer();

        if (_bufferedLength >= Utf8Bom.Length && _buffer.AsSpan(0, Utf8Bom.Length).SequenceEqual(Utf8Bom))
        {
            _recordStart = Utf8Bom.Length;
            _scanOffset = Utf8Bom.Length;
        }

        for (var line = 0; line < _skipLines && TrySkipPhysicalLine(); line++)
        {
            _skippedLineCount++;
        }
    }

    private bool TrySkipPhysicalLine()
    {
        while (true)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            var remaining = _buffer.AsSpan(_scanOffset, _bufferedLength - _scanOffset);
            var lineFeed = remaining.IndexOf((byte)'\n');
            var carriage = remaining.IndexOf((byte)'\r');
            var relativeTerminator = lineFeed < 0
                ? carriage
                : carriage < 0
                    ? lineFeed
                    : Math.Min(lineFeed, carriage);
            if (relativeTerminator >= 0)
            {
                var terminatorOffset = _scanOffset + relativeTerminator;
                var terminatorAbsoluteOffset = _bufferStartOffset + terminatorOffset;

                if (_buffer[terminatorOffset] == (byte)'\r' &&
                    _dialect.RecordEndingMode == SeparatedValuesRecordEndingMode.LfCrLf)
                {
                    if (terminatorOffset + 1 == _bufferedLength && !_endOfFile)
                        FillBuffer();
                    terminatorOffset = checked((int)(terminatorAbsoluteOffset - _bufferStartOffset));
                    if (terminatorOffset + 1 >= _bufferedLength ||
                        _buffer[terminatorOffset + 1] != (byte)'\n')
                        throw InvalidData("A physical preamble line contains a bare carriage return.");
                }
                else if (_buffer[terminatorOffset] == (byte)'\r' &&
                         _dialect.RecordEndingMode == SeparatedValuesRecordEndingMode.Any)
                {
                    if (terminatorOffset + 1 == _bufferedLength && !_endOfFile)
                        FillBuffer();
                }

                terminatorOffset = checked((int)(terminatorAbsoluteOffset - _bufferStartOffset));
                var contentEnd = terminatorOffset;
                if (contentEnd > _recordStart && _buffer[contentEnd - 1] == (byte)'\r')
                    contentEnd--;
                if (terminatorOffset + 1 < _bufferedLength &&
                    _buffer[terminatorOffset] == (byte)'\r' &&
                    _buffer[terminatorOffset + 1] == (byte)'\n')
                    terminatorOffset++;

                SeparatedValuesUtf8Reader.ValidateUtf8(
                    _buffer.AsSpan(_recordStart, contentEnd - _recordStart));
                _scanOffset = terminatorOffset + 1;
                _recordStart = _scanOffset;
                _state = ScanState.FieldStart;
                return true;
            }

            _scanOffset = _bufferedLength;
            if (_budgetExhausted)
                return false;

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
        if (length > _dialect.MaximumRecordBytes)
            throw InvalidData($"A separated-values record exceeds the {_dialect.MaximumRecordBytes:N0}-byte safety limit.");
        var end = terminatorOffset + terminatorLength;
        _scanOffset = end;
        _recordStart = end;
        _state = ScanState.FieldStart;

        if (length == 0 && _dialect.BlankRecordMode == SeparatedValuesBlankRecordMode.Skip)
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
            _bufferStartOffset + end,
            _dialect);
        return true;
    }

    private int GetRecordEndingLength()
    {
        if (_dialect.RecordEndingMode == SeparatedValuesRecordEndingMode.Any)
        {
            if (_scanOffset + 1 == _bufferedLength && !_endOfFile)
                FillBuffer();
            return _scanOffset + 1 < _bufferedLength && _buffer[_scanOffset + 1] == (byte)'\n'
                ? 2
                : 1;
        }

        if (_scanOffset + 1 == _bufferedLength && !_endOfFile)
            FillBuffer();

        return _scanOffset + 1 < _bufferedLength && _buffer[_scanOffset + 1] == (byte)'\n' ? 2 : 0;
    }

    private bool TrySkipComment()
    {
        if (_dialect.CommentPrefix.IsEmpty)
            return false;

        while (_bufferedLength - _scanOffset < _dialect.CommentPrefix.Length &&
               !_endOfFile &&
               !_budgetExhausted)
            FillBuffer();

        if (_bufferedLength - _scanOffset < _dialect.CommentPrefix.Length ||
            !_buffer.AsSpan(_scanOffset, _dialect.CommentPrefix.Length)
                .SequenceEqual(_dialect.CommentPrefix.AsSpan()))
            return false;

        var commentStartAbsoluteOffset = _bufferStartOffset + _scanOffset;
        _scanOffset += _dialect.CommentPrefix.Length;
        while (true)
        {
            var remaining = _buffer.AsSpan(_scanOffset, _bufferedLength - _scanOffset);
            var newline = remaining.IndexOf((byte)'\n');
            var carriage = remaining.IndexOf((byte)'\r');
            var terminator = newline < 0 ? carriage : carriage < 0 ? newline : Math.Min(newline, carriage);
            if (terminator >= 0)
            {
                var offset = _scanOffset + terminator;
                var terminatorAbsoluteOffset = _bufferStartOffset + offset;
                if (_buffer[offset] == (byte)'\r' &&
                    _dialect.RecordEndingMode == SeparatedValuesRecordEndingMode.LfCrLf)
                {
                    if (offset + 1 == _bufferedLength && !_endOfFile)
                        FillBuffer();
                    offset = checked((int)(terminatorAbsoluteOffset - _bufferStartOffset));
                    if (offset + 1 >= _bufferedLength || _buffer[offset + 1] != (byte)'\n')
                        throw InvalidData("A comment carriage return must be followed by a line feed.");
                }
                else if (_buffer[offset] == (byte)'\r' &&
                         _dialect.RecordEndingMode == SeparatedValuesRecordEndingMode.Any)
                {
                    if (offset + 1 == _bufferedLength && !_endOfFile)
                        FillBuffer();
                }

                offset = checked((int)(terminatorAbsoluteOffset - _bufferStartOffset));
                var commentStart = checked((int)(commentStartAbsoluteOffset - _bufferStartOffset));
                if (offset + 1 < _bufferedLength &&
                    _buffer[offset] == (byte)'\r' &&
                    _buffer[offset + 1] == (byte)'\n')
                    offset++;
                ValidateUtf8(_buffer.AsSpan(commentStart, offset - commentStart));
                _scanOffset = offset + 1;
                _recordStart = _scanOffset;
                _state = ScanState.FieldStart;
                return true;
            }

            _scanOffset = _bufferedLength;
            if (_budgetExhausted)
                return true;
            if (_endOfFile)
            {
                var commentStart = checked((int)(commentStartAbsoluteOffset - _bufferStartOffset));
                ValidateUtf8(_buffer.AsSpan(commentStart, _scanOffset - commentStart));
                _recordStart = _scanOffset;
                _state = ScanState.FieldStart;
                return true;
            }

            FillBuffer();
        }
    }

    private void FillBuffer()
    {
        if (_endOfFile)
            return;

        _cancellationToken.ThrowIfCancellationRequested();

        if (_bytesRead >= _maximumBytesRead ||
            (_deadlineTimestamp != long.MaxValue && Stopwatch.GetTimestamp() >= _deadlineTimestamp))
        {
            _budgetExhausted = true;
            return;
        }

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
                if (_buffer.Length >= Math.Min(
                        MaximumBufferSize,
                        Math.Min(_dialect.MaximumRecordBytes, _dialect.MaximumBufferedBytes)))
                    throw InvalidData(
                        $"A separated-values record exceeds the {_dialect.MaximumRecordBytes:N0}-byte safety limit.");

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
        var remainingBudget = _maximumBytesRead - _bytesRead;
        var requested = (int)Math.Min(capacity, Math.Min(remainingBytes, remainingBudget));
        if (requested <= 0)
        {
            _budgetExhausted = true;
            return;
        }
        var read = _stream.Read(_buffer, _bufferedLength, requested);
        if (read == 0)
        {
            _endOfFile = true;
            return;
        }

        _bufferedLength += read;
        _bytesRead += read;
        if (_bufferedLength - _recordStart > _dialect.MaximumRecordBytes)
            throw InvalidData($"A separated-values record exceeds the {_dialect.MaximumRecordBytes:N0}-byte safety limit.");
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

    internal static void ValidateUtf8(ReadOnlySpan<byte> bytes)
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
        AfterQuote,
        EscapedQuoted
    }
}

internal readonly ref struct SeparatedValuesUtf8Record
{
    private readonly ReadOnlySpan<byte> _bytes;
    private readonly byte _separator;
    private readonly SeparatedValuesDialect _dialect;

    public SeparatedValuesUtf8Record(
        ReadOnlySpan<byte> bytes,
        byte separator,
        long startOffset,
        long endOffset,
        SeparatedValuesDialect? dialect = null)
    {
        _bytes = bytes;
        _separator = separator;
        _dialect = dialect ?? SeparatedValuesDialect.Strict(separator);
        StartOffset = startOffset;
        EndOffset = endOffset;
    }

    public long StartOffset { get; }

    public long EndOffset { get; }

    public SeparatedValuesUtf8FieldEnumerator GetEnumerator()
    {
        return new SeparatedValuesUtf8FieldEnumerator(_bytes, _dialect);
    }

    public ReadOnlySpan<byte> Bytes => _bytes;
}

internal ref struct SeparatedValuesUtf8FieldEnumerator
{
    private readonly ReadOnlySpan<byte> _record;
    private readonly SeparatedValuesDialect _dialect;
    private readonly byte _separator;
    private int _nextOffset;
    private bool _finished;

    public SeparatedValuesUtf8FieldEnumerator(ReadOnlySpan<byte> record, SeparatedValuesDialect dialect)
    {
        _record = record;
        _dialect = dialect;
        _separator = dialect.Separator;
        _nextOffset = 0;
        _finished = false;
        Current = default;
    }

    public SeparatedValuesUtf8FieldEnumerator(ReadOnlySpan<byte> record, byte separator)
        : this(record, SeparatedValuesDialect.Strict(separator))
    {
    }

    public SeparatedValuesUtf8Field Current { get; private set; }

    public bool MoveNext()
    {
        if (_finished)
            return false;

        var start = _nextOffset;
        var trimmedStart = _dialect.WhitespaceMode == SeparatedValuesWhitespaceMode.Trim
            ? TrimStart(_record, start)
            : start;
        if (_dialect.Quote.HasValue && trimmedStart < _record.Length && _record[trimmedStart] == _dialect.Quote.Value)
        {
            var valueStart = trimmedStart + 1;
            var offset = valueStart;
            var needsUnescaping = false;

            while (offset < _record.Length)
            {
                if (_record[offset] != _dialect.Quote.Value)
                {
                    if (_dialect.EscapeMode == SeparatedValuesEscapeMode.Backslash &&
                        _record[offset] == (byte)'\\' &&
                        offset + 1 < _record.Length)
                    {
                        needsUnescaping = true;
                        offset += 2;
                        continue;
                    }
                    offset++;
                    continue;
                }

                if (_dialect.EscapeMode == SeparatedValuesEscapeMode.Double &&
                    offset + 1 < _record.Length &&
                    _record[offset + 1] == _dialect.Quote.Value)
                {
                    needsUnescaping = true;
                    offset += 2;
                    continue;
                }

                Current = new SeparatedValuesUtf8Field(
                    _record[valueStart..offset],
                    valueStart,
                    true,
                    needsUnescaping,
                    _dialect.EscapeMode,
                    false,
                    _dialect.Quote);
                SetNextOffset(offset + 1);
                return true;
            }

            throw new InvalidDataException("The quoted field is not terminated.");
        }

        var end = trimmedStart;
        while (end < _record.Length && _record[end] != _separator)
        {
            if (_dialect.Quote.HasValue && _record[end] == _dialect.Quote.Value)
                throw new InvalidDataException("A quote may only appear at the beginning of a field.");
            if (_record[end] is (byte)'\r' or (byte)'\n')
                throw new InvalidDataException("An unquoted field cannot contain a carriage return or line feed.");
            end++;
        }

        var unquotedStart = trimmedStart;
        var valueEnd = _dialect.WhitespaceMode == SeparatedValuesWhitespaceMode.Trim
            ? TrimEnd(_record, unquotedStart, end)
            : end;
        Current = new SeparatedValuesUtf8Field(
            _record[unquotedStart..valueEnd],
            unquotedStart,
            false,
            false,
            _dialect.EscapeMode,
            _dialect.IsNullToken(_record[unquotedStart..valueEnd], false),
            _dialect.Quote);
        SetNextOffset(end);
        return true;
    }

    private void SetNextOffset(int endOffset)
    {
        if (_dialect.WhitespaceMode == SeparatedValuesWhitespaceMode.Trim)
        {
            while (endOffset < _record.Length && IsWhitespace(_record[endOffset]))
                endOffset++;
        }

        if (endOffset == _record.Length)
        {
            _finished = true;
            return;
        }

        if (_record[endOffset] != _separator)
            throw new InvalidDataException("A field was not followed by the configured separator.");

        _nextOffset = endOffset + 1;
    }

    private static int TrimStart(ReadOnlySpan<byte> record, int start)
    {
        while (start < record.Length && IsWhitespace(record[start]))
            start++;
        return start;
    }

    private static int TrimEnd(ReadOnlySpan<byte> record, int start, int end)
    {
        while (end > start && IsWhitespace(record[end - 1]))
            end--;
        return end;
    }

    private static bool IsWhitespace(byte value)
    {
        return value is (byte)' ' or (byte)'\t';
    }
}

internal readonly ref struct SeparatedValuesUtf8Field
{
    public SeparatedValuesUtf8Field(
        ReadOnlySpan<byte> encodedValue,
        int encodedOffset,
        bool wasQuoted,
        bool needsUnescaping,
        SeparatedValuesEscapeMode escapeMode = SeparatedValuesEscapeMode.Double,
        bool isNullToken = false,
        byte? quote = (byte)'"')
    {
        EncodedValue = encodedValue;
        EncodedOffset = encodedOffset;
        WasQuoted = wasQuoted;
        NeedsUnescaping = needsUnescaping;
        EscapeMode = escapeMode;
        IsNullToken = isNullToken;
        Quote = quote;
    }

    public ReadOnlySpan<byte> EncodedValue { get; }

    public int EncodedOffset { get; }

    public bool WasQuoted { get; }

    public bool NeedsUnescaping { get; }

    public SeparatedValuesEscapeMode EscapeMode { get; }

    public bool IsNullToken { get; }

    public byte? Quote { get; }

    public string Decode()
    {
        if (!NeedsUnescaping)
            return Encoding.UTF8.GetString(EncodedValue);

        var destination = GC.AllocateUninitializedArray<byte>(EncodedValue.Length);
        var written = 0;

        for (var offset = 0; offset < EncodedValue.Length; offset++)
        {
            var value = EncodedValue[offset];
            if (EscapeMode == SeparatedValuesEscapeMode.Double &&
                Quote.HasValue &&
                value == Quote.Value &&
                offset + 1 < EncodedValue.Length &&
                EncodedValue[offset + 1] == Quote.Value)
                offset++;
            else if (EscapeMode == SeparatedValuesEscapeMode.Backslash &&
                     value == (byte)'\\' &&
                     offset + 1 < EncodedValue.Length)
                value = EncodedValue[++offset];
            destination[written++] = value;
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
            if (expectedOffset == expected.Length)
                return false;
            var value = EncodedValue[offset];
            if (EscapeMode == SeparatedValuesEscapeMode.Double &&
                Quote.HasValue &&
                value == Quote.Value &&
                offset + 1 < EncodedValue.Length &&
                EncodedValue[offset + 1] == Quote.Value)
                offset++;
            else if (EscapeMode == SeparatedValuesEscapeMode.Backslash &&
                     value == (byte)'\\' &&
                     offset + 1 < EncodedValue.Length)
                value = EncodedValue[++offset];
            if (value != expected[expectedOffset])
                return false;
            expectedOffset++;
        }

        return expectedOffset == expected.Length;
    }
}
