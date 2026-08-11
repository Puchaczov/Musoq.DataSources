using System;
using System.Buffers;
using System.IO;
using System.Text;

namespace Musoq.DataSources.Git;

/// <summary>
/// Small pooled reader for Git's NUL-delimited UTF-8 protocols. A completed large token is returned to the pool
/// immediately, so streaming readers do not retain a large path or commit message for the rest of a traversal.
/// </summary>
internal sealed class GitNulDelimitedUtf8Reader : IDisposable
{
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private readonly Stream _stream;
    private readonly byte[] _buffer = ArrayPool<byte>.Shared.Rent(16 * 1024);
    private byte[]? _tokenBuffer;
    private int _length;
    private int _offset;
    private int _tokenLength;
    private bool _disposed;

    public GitNulDelimitedUtf8Reader(Stream stream)
    {
        _stream = stream;
    }

    public string? ReadToken()
    {
        _tokenLength = 0;
        while (true)
        {
            if (_offset == _length)
            {
                _length = _stream.Read(_buffer, 0, _buffer.Length);
                _offset = 0;
                if (_length == 0)
                    return _tokenLength == 0 ? null : DecodeBufferedToken();
            }

            var delimiterOffset = Array.IndexOf(_buffer, (byte)0, _offset, _length - _offset);
            if (delimiterOffset >= 0)
            {
                var count = delimiterOffset - _offset;
                if (_tokenLength == 0)
                {
                    var token = Utf8.GetString(_buffer, _offset, count);
                    _offset = delimiterOffset + 1;
                    return token;
                }

                Append(_buffer, _offset, count);
                _offset = delimiterOffset + 1;
                return DecodeBufferedToken();
            }

            Append(_buffer, _offset, _length - _offset);
            _offset = _length;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        ArrayPool<byte>.Shared.Return(_buffer);
        if (_tokenBuffer is not null)
            ArrayPool<byte>.Shared.Return(_tokenBuffer);
    }

    private void Append(byte[] bytes, int offset, int count)
    {
        if (count == 0)
            return;

        if (_tokenBuffer is null || _tokenBuffer.Length < _tokenLength + count)
        {
            var currentLength = _tokenBuffer?.Length ?? 0;
            var nextLength = Math.Max(_tokenLength + count, Math.Max(256, currentLength * 2));
            var next = ArrayPool<byte>.Shared.Rent(nextLength);
            if (_tokenBuffer is not null)
            {
                Buffer.BlockCopy(_tokenBuffer, 0, next, 0, _tokenLength);
                ArrayPool<byte>.Shared.Return(_tokenBuffer);
            }

            _tokenBuffer = next;
        }

        Buffer.BlockCopy(bytes, offset, _tokenBuffer!, _tokenLength, count);
        _tokenLength += count;
    }

    private string DecodeBufferedToken()
    {
        var buffer = _tokenBuffer!;
        var token = Utf8.GetString(buffer, 0, _tokenLength);
        ArrayPool<byte>.Shared.Return(buffer);
        _tokenBuffer = null;
        _tokenLength = 0;
        return token;
    }
}
