#nullable enable

using System;
using System.Buffers;
using System.IO;
using System.Text;
using System.Threading;

using Musoq.DataSources.Search.Components.Diagnostics;
using Musoq.DataSources.Search.Components.Testing;

namespace Musoq.DataSources.Search.Components.Text;

internal static class SearchTextReader
{
    private const int ProbeLength = 4;

    public static TextReader Open(string path, SearchEncodingMode mode)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        SearchTestHooks.BeforeContentOpen(path);
        var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            SearchCharBuffer.RequestedLength,
            FileOptions.SequentialScan);

        try
        {
            var resolution = Resolve(stream, mode);
            stream.Position = resolution.DataOffset;
            return new MappedTextReader(
                stream,
                resolution.Encoding,
                resolution.DataOffset,
                resolution.Tolerant,
                resolution.LegacyEncoding);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Opens a bounded in-memory byte snapshot with the same encoding and
    ///     coordinate rules as a file-backed reader. The supplied array is
    ///     borrowed until the returned reader is disposed.
    /// </summary>
    internal static TextReader Open(byte[] bytes, int length, SearchEncodingMode mode)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (length < 0 || length > bytes.Length)
            throw new ArgumentOutOfRangeException(nameof(length));

        var stream = new MemoryStream(
            bytes,
            index: 0,
            count: length,
            writable: false,
            publiclyVisible: true);
        try
        {
            var resolution = Resolve(stream, mode);
            stream.Position = resolution.DataOffset;
            return new MappedTextReader(
                stream,
                resolution.Encoding,
                resolution.DataOffset,
                resolution.Tolerant,
                resolution.LegacyEncoding);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    private static Resolution Resolve(Stream stream, SearchEncodingMode mode)
    {
        Span<byte> prefix = stackalloc byte[ProbeLength];
        var bytesRead = ReadPrefix(stream, prefix);
        var detected = DetectBom(prefix[..bytesRead]);

        if (detected == DetectedEncoding.Unsupported && !IsLegacy(mode))
            throw new SearchEncodingException(SearchDiagnosticCatalog.UnsupportedEncoding());

        return mode switch
        {
            SearchEncodingMode.Auto => ResolveAuto(detected),
            SearchEncodingMode.Utf8 => ResolveExplicitUtf8(detected),
            SearchEncodingMode.Utf8Bom => ResolveExplicit(
                detected,
                DetectedEncoding.Utf8Bom,
                DecodedEncoding.Utf8,
                Utf8BomLength),
            SearchEncodingMode.Utf16LittleEndianBom => ResolveExplicit(
                detected,
                DetectedEncoding.Utf16LittleEndianBom,
                DecodedEncoding.Utf16LittleEndian,
                Utf16BomLength),
            SearchEncodingMode.Utf16BigEndianBom => ResolveExplicit(
                detected,
                DetectedEncoding.Utf16BigEndianBom,
                DecodedEncoding.Utf16BigEndian,
                Utf16BomLength),
            SearchEncodingMode.Windows1250 or
                SearchEncodingMode.Windows1252 or
                SearchEncodingMode.Iso88592 => ResolveLegacy(mode),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }

    private static bool IsLegacy(SearchEncodingMode mode)
    {
        return mode is SearchEncodingMode.Windows1250 or
            SearchEncodingMode.Windows1252 or
            SearchEncodingMode.Iso88592;
    }

    private static Resolution ResolveAuto(DetectedEncoding detected)
    {
        return detected switch
        {
            DetectedEncoding.None => new Resolution(
                DecodedEncoding.Utf8,
                0,
                Tolerant: true),
            DetectedEncoding.Utf8Bom => new Resolution(
                DecodedEncoding.Utf8,
                Utf8BomLength,
                Tolerant: true),
            DetectedEncoding.Utf16LittleEndianBom => new Resolution(
                DecodedEncoding.Utf16LittleEndian,
                Utf16BomLength,
                Tolerant: true),
            DetectedEncoding.Utf16BigEndianBom => new Resolution(
                DecodedEncoding.Utf16BigEndian,
                Utf16BomLength,
                Tolerant: true),
            _ => throw new ArgumentOutOfRangeException(nameof(detected))
        };
    }

    private static Resolution ResolveExplicitUtf8(DetectedEncoding detected)
    {
        if (detected != DetectedEncoding.None)
        {
            throw new SearchEncodingException(
                SearchDiagnosticCatalog.EncodingMismatch());
        }

        return new Resolution(
            DecodedEncoding.Utf8,
            0,
            Tolerant: false);
    }

    private static Resolution ResolveExplicit(
        DetectedEncoding detected,
        DetectedEncoding expected,
        DecodedEncoding encoding,
        int dataOffset)
    {
        if (detected != expected)
        {
            throw new SearchEncodingException(
                SearchDiagnosticCatalog.EncodingMismatch());
        }

        return new Resolution(encoding, dataOffset, Tolerant: false);
    }

    private static Resolution ResolveLegacy(SearchEncodingMode mode)
    {
        return new Resolution(
            DecodedEncoding.SingleByte,
            0,
            Tolerant: false,
            LegacyEncoding: SearchEncodingPolicy.CreateLegacyEncoding(mode));
    }

    private static int ReadPrefix(Stream stream, Span<byte> destination)
    {
        var total = 0;
        while (total < destination.Length)
        {
            var bytesRead = stream.Read(destination[total..]);
            if (bytesRead == 0)
                break;

            total += bytesRead;
        }

        return total;
    }

    private static DetectedEncoding DetectBom(ReadOnlySpan<byte> prefix)
    {
        if (StartsWith(prefix, [0xff, 0xfe, 0x00, 0x00]) ||
            StartsWith(prefix, [0x00, 0x00, 0xfe, 0xff]))
        {
            return DetectedEncoding.Unsupported;
        }

        if (StartsWith(prefix, [0xef, 0xbb, 0xbf]))
            return DetectedEncoding.Utf8Bom;

        if (StartsWith(prefix, [0xff, 0xfe]))
            return DetectedEncoding.Utf16LittleEndianBom;

        if (StartsWith(prefix, [0xfe, 0xff]))
            return DetectedEncoding.Utf16BigEndianBom;

        return DetectedEncoding.None;
    }

    private static bool StartsWith(ReadOnlySpan<byte> value, ReadOnlySpan<byte> prefix)
    {
        return value.Length >= prefix.Length && value[..prefix.Length].SequenceEqual(prefix);
    }

    private const int Utf8BomLength = 3;
    private const int Utf16BomLength = 2;

    private enum DetectedEncoding
    {
        None,
        Utf8Bom,
        Utf16LittleEndianBom,
        Utf16BigEndianBom,
        Unsupported
    }

    private enum DecodedEncoding
    {
        Utf8,
        Utf16LittleEndian,
        Utf16BigEndian,
        SingleByte
    }

    private readonly record struct Resolution(
        DecodedEncoding Encoding,
        int DataOffset,
        bool Tolerant,
        System.Text.Encoding? LegacyEncoding = null);

    private readonly record struct RawByte(byte Value, long Offset);

    private sealed class MappedTextReader(
        Stream stream,
        DecodedEncoding encoding,
        long initialByteOffset,
        bool tolerant,
        System.Text.Encoding? legacyEncoding)
        : TextReader, ISearchTextCoordinateReader
    {
        private const int RawBufferLength = SearchCharBuffer.RequestedLength;
        private const char ReplacementCharacter = '\ufffd';

        private Stream? _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        private byte[]? _rawBuffer = ArrayPool<byte>.Shared.Rent(RawBufferLength);
        private SearchCharCoordinate[] _coordinates =
            ArrayPool<SearchCharCoordinate>.Shared.Rent(SearchCharBuffer.RequestedLength);
        private readonly DecodedEncoding _encoding = encoding;
        private readonly bool _tolerant = tolerant;
        private readonly System.Text.Encoding? _legacyEncoding = legacyEncoding;
        private readonly RawByte[] _pendingBytes = new RawByte[8];
        private int _pendingByteCount;
        private int _rawIndex;
        private int _rawCount;
        private long _rawBufferOffset = initialByteOffset;
        private long _nextRawOffset = initialByteOffset;
        private bool _endOfStream;
        private bool _hasPendingCharacter;
        private char _pendingCharacter;
        private SearchCharCoordinate _pendingCoordinate;

        public long InitialByteOffset { get; } = initialByteOffset;

        public SearchCharCoordinate[] LastReadCoordinates => _coordinates;

        public int LastReadCoordinateCount { get; private set; }

        public override int Read(char[] buffer, int index, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            if (index > buffer.Length - count)
                throw new ArgumentException("The read range exceeds the supplied character buffer.");

            if (_stream is null)
                throw new ObjectDisposedException(nameof(MappedTextReader));

            LastReadCoordinateCount = 0;
            if (count == 0)
                return 0;

            EnsureCoordinateCapacity(count);
            var charsRead = 0;
            while (charsRead < count && TryReadCharacter(
                       out var character,
                       out var coordinate))
            {
                buffer[index + charsRead] = character;
                _coordinates[charsRead] = coordinate;
                charsRead++;
            }

            SearchTestHooks.Checkpoint();
            LastReadCoordinateCount = charsRead;
            return charsRead;
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            var stream = Interlocked.Exchange(ref _stream, null);
            stream?.Dispose();

            var rawBuffer = Interlocked.Exchange(ref _rawBuffer, null);
            if (rawBuffer is not null)
                ArrayPool<byte>.Shared.Return(rawBuffer);

            var coordinates = Interlocked.Exchange(
                ref _coordinates,
                Array.Empty<SearchCharCoordinate>());
            if (coordinates.Length > 0)
                ArrayPool<SearchCharCoordinate>.Shared.Return(coordinates);

            base.Dispose(disposing);
        }

        private bool TryReadCharacter(
            out char character,
            out SearchCharCoordinate coordinate)
        {
            if (_hasPendingCharacter)
            {
                _hasPendingCharacter = false;
                character = _pendingCharacter;
                coordinate = _pendingCoordinate;
                return true;
            }

            return _encoding switch
            {
                DecodedEncoding.Utf8 => TryReadUtf8Character(out character, out coordinate),
                DecodedEncoding.Utf16LittleEndian => TryReadUtf16Character(
                    bigEndian: false,
                    out character,
                    out coordinate),
                DecodedEncoding.Utf16BigEndian => TryReadUtf16Character(
                    bigEndian: true,
                    out character,
                    out coordinate),
                DecodedEncoding.SingleByte => TryReadSingleByteCharacter(
                    out character,
                    out coordinate),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private bool TryReadSingleByteCharacter(
            out char character,
            out SearchCharCoordinate coordinate)
        {
            if (!TryReadByte(out var value))
            {
                character = default;
                coordinate = default;
                return false;
            }

            Span<byte> bytes = stackalloc byte[1];
            Span<char> characters = stackalloc char[1];
            bytes[0] = value.Value;
            var charsWritten = (_legacyEncoding ?? throw new InvalidOperationException(
                    "A legacy encoding is required for single-byte decoding."))
                .GetChars(bytes, characters);
            if (charsWritten != 1)
                throw new DecoderFallbackException(
                    "The selected single-byte encoding did not produce one character.");

            character = characters[0];
            coordinate = MappedCoordinate(value.Offset, 1, true, true);
            return true;
        }

        private bool TryReadUtf8Character(
            out char character,
            out SearchCharCoordinate coordinate)
        {
            if (!TryReadByte(out var first))
            {
                character = default;
                coordinate = default;
                return false;
            }

            var requiredContinuationBytes = first.Value switch
            {
                <= 0x7f => 0,
                >= 0xc2 and <= 0xdf => 1,
                >= 0xe0 and <= 0xef => 2,
                >= 0xf0 and <= 0xf4 => 3,
                _ => -1
            };

            if (requiredContinuationBytes < 0)
            {
                return HandleInvalidUtf8(
                    first,
                    out character,
                    out coordinate);
            }

            if (requiredContinuationBytes == 0)
            {
                character = (char)first.Value;
                coordinate = MappedCoordinate(first.Offset, 1, true, true);
                return true;
            }

            Span<RawByte> continuationBytes = stackalloc RawByte[3];
            var continuationCount = 0;
            while (continuationCount < requiredContinuationBytes)
            {
                if (!TryReadByte(out continuationBytes[continuationCount]))
                {
                    PushBack(continuationBytes[..continuationCount]);
                    return HandleInvalidUtf8(
                        first,
                        out character,
                        out coordinate);
                }

                continuationCount++;
            }

            if (!IsValidUtf8Sequence(first.Value, continuationBytes[..continuationCount]))
            {
                PushBack(continuationBytes[..continuationCount]);
                return HandleInvalidUtf8(
                    first,
                    out character,
                    out coordinate);
            }

            var scalar = first.Value & (requiredContinuationBytes switch
            {
                1 => 0x1f,
                2 => 0x0f,
                _ => 0x07
            });
            for (var index = 0; index < continuationCount; index++)
                scalar = (scalar << 6) | (continuationBytes[index].Value & 0x3f);

            var byteLength = checked(requiredContinuationBytes + 1);
            if (scalar <= char.MaxValue)
            {
                character = (char)scalar;
                coordinate = MappedCoordinate(first.Offset, byteLength, true, true);
                return true;
            }

            scalar -= 0x10000;
            var high = (char)(0xd800 + (scalar >> 10));
            var low = (char)(0xdc00 + (scalar & 0x3ff));
            var highCoordinate = MappedCoordinate(
                first.Offset,
                byteLength,
                canStart: true,
                canEnd: false);
            _pendingCharacter = low;
            _pendingCoordinate = MappedCoordinate(
                first.Offset,
                byteLength,
                canStart: false,
                canEnd: true);
            _hasPendingCharacter = true;
            character = high;
            coordinate = highCoordinate;
            return true;
        }

        private bool TryReadUtf16Character(
            bool bigEndian,
            out char character,
            out SearchCharCoordinate coordinate)
        {
            if (!TryReadCodeUnit(out var first, out var firstByte, out var trailingByte))
            {
                if (trailingByte.HasValue)
                {
                    return HandleInvalidUtf16(
                        trailingByte.Value,
                        out character,
                        out coordinate);
                }

                character = default;
                coordinate = default;
                return false;
            }

            var firstCharacter = (char)first;
            if (!char.IsSurrogate(firstCharacter))
            {
                character = firstCharacter;
                coordinate = MappedCoordinate(firstByte.Offset, 2, true, true);
                return true;
            }

            if (char.IsLowSurrogate(firstCharacter))
            {
                return HandleInvalidUtf16(
                    new RawByte(0, firstByte.Offset),
                    byteLength: 2,
                    out character,
                    out coordinate);
            }

            if (!TryReadCodeUnit(out var second, out var secondByte, out var secondTrailingByte))
            {
                if (secondTrailingByte.HasValue)
                    PushBack([secondTrailingByte.Value]);

                return HandleInvalidUtf16(
                    new RawByte(0, firstByte.Offset),
                    byteLength: 2,
                    out character,
                    out coordinate);
            }

            var secondCharacter = (char)second;
            if (!char.IsLowSurrogate(secondCharacter))
            {
                PushBack([
                    new RawByte((byte)(bigEndian ? second >> 8 : second & 0xff), secondByte.Offset),
                    new RawByte((byte)(bigEndian ? second & 0xff : second >> 8), secondByte.Offset + 1)
                ]);
                return HandleInvalidUtf16(
                    new RawByte(0, firstByte.Offset),
                    byteLength: 2,
                    out character,
                    out coordinate);
            }

            var highCoordinate = MappedCoordinate(
                firstByte.Offset,
                4,
                canStart: true,
                canEnd: false);
            _pendingCharacter = secondCharacter;
            _pendingCoordinate = MappedCoordinate(
                firstByte.Offset,
                4,
                canStart: false,
                canEnd: true);
            _hasPendingCharacter = true;
            character = firstCharacter;
            coordinate = highCoordinate;
            return true;
        }

        private bool TryReadCodeUnit(
            out ushort value,
            out RawByte firstByte,
            out RawByte? trailingByte)
        {
            trailingByte = null;
            if (!TryReadByte(out firstByte))
            {
                value = default;
                return false;
            }

            if (!TryReadByte(out var secondByte))
            {
                trailingByte = firstByte;
                value = default;
                return false;
            }

            value = _encoding == DecodedEncoding.Utf16BigEndian
                ? (ushort)((firstByte.Value << 8) | secondByte.Value)
                : (ushort)(firstByte.Value | (secondByte.Value << 8));
            return true;
        }

        private bool HandleInvalidUtf8(
            RawByte first,
            out char character,
            out SearchCharCoordinate coordinate)
        {
            if (!_tolerant)
                throw new DecoderFallbackException(
                    $"Unable to decode UTF-8 byte at index {first.Offset}.");

            character = ReplacementCharacter;
            coordinate = UnmappedCoordinate(first.Offset, 1);
            return true;
        }

        private bool HandleInvalidUtf16(
            RawByte first,
            out char character,
            out SearchCharCoordinate coordinate)
        {
            if (!_tolerant)
                throw new DecoderFallbackException(
                    $"Unable to decode UTF-16 bytes at index {first.Offset}.");

            character = ReplacementCharacter;
            coordinate = UnmappedCoordinate(first.Offset, 1);
            return true;
        }

        private bool HandleInvalidUtf16(
            RawByte first,
            int byteLength,
            out char character,
            out SearchCharCoordinate coordinate)
        {
            if (!_tolerant)
                throw new DecoderFallbackException(
                    $"Unable to decode UTF-16 bytes at index {first.Offset}.");

            character = ReplacementCharacter;
            coordinate = UnmappedCoordinate(first.Offset, byteLength);
            return true;
        }

        private static bool IsValidUtf8Sequence(
            byte first,
            ReadOnlySpan<RawByte> continuationBytes)
        {
            for (var index = 0; index < continuationBytes.Length; index++)
            {
                if ((continuationBytes[index].Value & 0xc0) != 0x80)
                    return false;
            }

            if (first == 0xe0 && continuationBytes[0].Value < 0xa0)
                return false;

            if (first == 0xed && continuationBytes[0].Value >= 0xa0)
                return false;

            if (first == 0xf0 && continuationBytes[0].Value < 0x90)
                return false;

            if (first == 0xf4 && continuationBytes[0].Value > 0x8f)
                return false;

            return true;
        }

        private bool TryReadByte(out RawByte value)
        {
            if (_pendingByteCount > 0)
            {
                value = _pendingBytes[0];
                Array.Copy(
                    _pendingBytes,
                    1,
                    _pendingBytes,
                    0,
                    --_pendingByteCount);
                return true;
            }

            if (_endOfStream)
            {
                value = default;
                return false;
            }

            if (_rawIndex >= _rawCount)
            {
                var stream = _stream ?? throw new ObjectDisposedException(nameof(MappedTextReader));
                var rawBuffer = _rawBuffer ?? throw new ObjectDisposedException(nameof(MappedTextReader));
                _rawCount = stream.Read(rawBuffer, 0, rawBuffer.Length);
                _rawIndex = 0;
                _rawBufferOffset = _nextRawOffset;
                _nextRawOffset = checked(_nextRawOffset + _rawCount);
                if (_rawCount == 0)
                {
                    _endOfStream = true;
                    value = default;
                    return false;
                }
            }

            var buffer = _rawBuffer ?? throw new ObjectDisposedException(nameof(MappedTextReader));
            value = new RawByte(
                buffer[_rawIndex],
                _rawBufferOffset + _rawIndex);
            _rawIndex++;
            return true;
        }

        private void PushBack(ReadOnlySpan<RawByte> values)
        {
            if (values.Length == 0)
                return;

            if (_pendingByteCount + values.Length > _pendingBytes.Length)
                throw new InvalidOperationException("The UTF decoder pushback buffer is full.");

            Array.Copy(
                _pendingBytes,
                0,
                _pendingBytes,
                values.Length,
                _pendingByteCount);
            values.CopyTo(_pendingBytes);
            _pendingByteCount += values.Length;
        }

        private static SearchCharCoordinate MappedCoordinate(
            long byteOffset,
            long byteLength,
            bool canStart,
            bool canEnd)
        {
            return new SearchCharCoordinate(
                IsMapped: true,
                ByteOffset: byteOffset,
                ByteLength: byteLength,
                CanStart: canStart,
                CanEnd: canEnd);
        }

        private static SearchCharCoordinate UnmappedCoordinate(
            long byteOffset,
            long byteLength)
        {
            return new SearchCharCoordinate(
                IsMapped: false,
                ByteOffset: byteOffset,
                ByteLength: byteLength,
                CanStart: false,
                CanEnd: false);
        }

        private void EnsureCoordinateCapacity(int requiredLength)
        {
            if (_coordinates.Length >= requiredLength)
                return;

            var replacement = ArrayPool<SearchCharCoordinate>.Shared.Rent(requiredLength);
            var previous = Interlocked.Exchange(ref _coordinates, replacement);
            ArrayPool<SearchCharCoordinate>.Shared.Return(previous);
        }
    }
}
