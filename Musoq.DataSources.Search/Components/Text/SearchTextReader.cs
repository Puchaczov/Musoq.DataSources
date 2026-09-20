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
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);
    private static readonly Encoding StrictUtf16LittleEndian = new UnicodeEncoding(
        bigEndian: false,
        byteOrderMark: false,
        throwOnInvalidBytes: true);
    private static readonly Encoding StrictUtf16BigEndian = new UnicodeEncoding(
        bigEndian: true,
        byteOrderMark: false,
        throwOnInvalidBytes: true);

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
            var reader = new StreamReader(
                stream,
                resolution.Encoding,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: SearchCharBuffer.RequestedLength,
                leaveOpen: false);
            return new MappedTextReader(
                reader,
                resolution.EncodingKind,
                resolution.DataOffset);
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
            var reader = new StreamReader(
                stream,
                resolution.Encoding,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: SearchCharBuffer.RequestedLength,
                leaveOpen: false);
            return new MappedTextReader(
                reader,
                resolution.EncodingKind,
                resolution.DataOffset);
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

        if (detected == DetectedEncoding.Unsupported)
            throw new SearchEncodingException(SearchDiagnosticCatalog.UnsupportedEncoding());

        return mode switch
        {
            SearchEncodingMode.Auto => ResolveAuto(detected),
            SearchEncodingMode.Utf8 => ResolveExplicitUtf8(detected),
            SearchEncodingMode.Utf8Bom => ResolveExplicit(
                detected,
                DetectedEncoding.Utf8Bom,
                StrictUtf8,
                Utf8BomLength),
            SearchEncodingMode.Utf16LittleEndianBom => ResolveExplicit(
                detected,
                DetectedEncoding.Utf16LittleEndianBom,
                StrictUtf16LittleEndian,
                Utf16BomLength),
            SearchEncodingMode.Utf16BigEndianBom => ResolveExplicit(
                detected,
                DetectedEncoding.Utf16BigEndianBom,
                StrictUtf16BigEndian,
                Utf16BomLength),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }

    private static Resolution ResolveAuto(DetectedEncoding detected)
    {
        return detected switch
        {
            DetectedEncoding.None => new Resolution(StrictUtf8, 0, DecodedEncoding.Utf8),
            DetectedEncoding.Utf8Bom => new Resolution(
                StrictUtf8,
                Utf8BomLength,
                DecodedEncoding.Utf8),
            DetectedEncoding.Utf16LittleEndianBom => new Resolution(
                StrictUtf16LittleEndian,
                Utf16BomLength,
                DecodedEncoding.Utf16),
            DetectedEncoding.Utf16BigEndianBom => new Resolution(
                StrictUtf16BigEndian,
                Utf16BomLength,
                DecodedEncoding.Utf16),
            _ => throw new ArgumentOutOfRangeException(nameof(detected))
        };
    }

    private static Resolution ResolveExplicitUtf8(DetectedEncoding detected)
    {
        if (detected != DetectedEncoding.None)
        {
            throw new SearchEncodingException(SearchDiagnosticCatalog.EncodingMismatch());
        }

        return new Resolution(StrictUtf8, 0, DecodedEncoding.Utf8);
    }

    private static Resolution ResolveExplicit(
        DetectedEncoding detected,
        DetectedEncoding expected,
        Encoding encoding,
        int dataOffset)
    {
        if (detected != expected)
        {
            throw new SearchEncodingException(SearchDiagnosticCatalog.EncodingMismatch());
        }

        var decodedEncoding = encoding == StrictUtf8
            ? DecodedEncoding.Utf8
            : DecodedEncoding.Utf16;
        return new Resolution(encoding, dataOffset, decodedEncoding);
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

    private sealed class MappedTextReader(
        StreamReader reader,
        DecodedEncoding encoding,
        long initialByteOffset)
        : TextReader, ISearchTextCoordinateReader
    {
        private StreamReader? _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        private SearchCharCoordinate[] _coordinates =
            ArrayPool<SearchCharCoordinate>.Shared.Rent(SearchCharBuffer.RequestedLength);
        private readonly DecodedEncoding _encoding = encoding;
        private long _nextByteOffset = initialByteOffset;
        private long _pendingUtf8SurrogateOffset;
        private bool _hasPendingUtf8Surrogate;

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

            var reader = _reader ?? throw new ObjectDisposedException(nameof(MappedTextReader));
            var charsRead = reader.Read(buffer, index, count);
            SearchTestHooks.Checkpoint();
            LastReadCoordinateCount = charsRead;
            if (charsRead == 0)
                return 0;

            EnsureCoordinateCapacity(charsRead);
            PopulateCoordinates(buffer.AsSpan(index, charsRead));
            return charsRead;
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            var reader = Interlocked.Exchange(ref _reader, null);
            reader?.Dispose();
            var coordinates = Interlocked.Exchange(
                ref _coordinates,
                Array.Empty<SearchCharCoordinate>());
            if (coordinates.Length > 0)
                ArrayPool<SearchCharCoordinate>.Shared.Return(coordinates);

            base.Dispose(disposing);
        }

        private void EnsureCoordinateCapacity(int requiredLength)
        {
            if (_coordinates.Length >= requiredLength)
                return;

            var replacement = ArrayPool<SearchCharCoordinate>.Shared.Rent(requiredLength);
            var previous = Interlocked.Exchange(ref _coordinates, replacement);
            ArrayPool<SearchCharCoordinate>.Shared.Return(previous);
        }

        private void PopulateCoordinates(ReadOnlySpan<char> characters)
        {
            for (var index = 0; index < characters.Length; index++)
            {
                var current = characters[index];
                _coordinates[index] = _encoding switch
                {
                    DecodedEncoding.Utf8 => CoordinateUtf8(current),
                    DecodedEncoding.Utf16 => CoordinateUtf16(),
                    _ => throw new ArgumentOutOfRangeException()
                };
            }
        }

        private SearchCharCoordinate CoordinateUtf8(char current)
        {
            if (_hasPendingUtf8Surrogate)
            {
                if (!char.IsLowSurrogate(current))
                {
                    throw new InvalidDataException(
                        "The strict UTF-8 decoder returned an unpaired surrogate.");
                }

                _hasPendingUtf8Surrogate = false;
                _nextByteOffset = checked(_nextByteOffset + 4);
                return new SearchCharCoordinate(
                    IsMapped: true,
                    ByteOffset: _pendingUtf8SurrogateOffset,
                    ByteLength: 4,
                    CanStart: false,
                    CanEnd: true);
            }

            if (char.IsHighSurrogate(current))
            {
                _hasPendingUtf8Surrogate = true;
                _pendingUtf8SurrogateOffset = _nextByteOffset;
                return new SearchCharCoordinate(
                    IsMapped: true,
                    ByteOffset: _nextByteOffset,
                    ByteLength: 4,
                    CanStart: true,
                    CanEnd: false);
            }

            if (char.IsLowSurrogate(current))
            {
                throw new InvalidDataException(
                    "The strict UTF-8 decoder returned an unpaired surrogate.");
            }

            var byteLength = Utf8ByteLength(current);
            var coordinate = new SearchCharCoordinate(
                IsMapped: true,
                ByteOffset: _nextByteOffset,
                ByteLength: byteLength,
                CanStart: true,
                CanEnd: true);
            _nextByteOffset = checked(_nextByteOffset + byteLength);
            return coordinate;
        }

        private SearchCharCoordinate CoordinateUtf16()
        {
            var coordinate = new SearchCharCoordinate(
                IsMapped: true,
                ByteOffset: _nextByteOffset,
                ByteLength: 2,
                CanStart: true,
                CanEnd: true);
            _nextByteOffset = checked(_nextByteOffset + 2);
            return coordinate;
        }

        private static int Utf8ByteLength(char value)
        {
            return value switch
            {
                <= '\u007f' => 1,
                <= '\u07ff' => 2,
                _ => 3
            };
        }
    }

    private enum DecodedEncoding
    {
        Utf8,
        Utf16
    }

    private readonly record struct Resolution(
        Encoding Encoding,
        int DataOffset,
        DecodedEncoding EncodingKind);
}
