#nullable enable

using System;
using System.Buffers;
using System.IO;
using System.Threading;

using Musoq.DataSources.Search.Components.Diagnostics;
using Musoq.DataSources.Search.Components.Testing;

namespace Musoq.DataSources.Search.Components.Traversal;

/// <summary>
///     One pooled byte snapshot used by the small-file adaptive path. The
///     snapshot owns both its process-wide memory credits and its pooled array.
/// </summary>
internal sealed class SearchSmallFileBuffer : IDisposable
{
    private byte[]? _array;
    private readonly long _creditedBytes;

    private SearchSmallFileBuffer(byte[] array, int length, long creditedBytes)
    {
        _array = array;
        Length = length;
        _creditedBytes = creditedBytes;
    }

    public byte[] Array => Volatile.Read(ref _array) ?? throw new ObjectDisposedException(nameof(SearchSmallFileBuffer));

    public int Length { get; }

    public static SearchSmallFileBuffer Read(
        string path,
        long expectedLength,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (expectedLength < 0 || expectedLength > 4L * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(expectedLength));

        SearchTestHooks.BeforeContentOpen(path);
        var length = checked((int)expectedLength);
        var creditedBytes = Math.Max(1L, expectedLength);
        SearchMemoryCreditGate.Acquire(creditedBytes, cancellationToken);
        var array = ArrayPool<byte>.Shared.Rent(Math.Max(1, length));
        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 64 * 1024,
                FileOptions.SequentialScan);
            var offset = 0;
            while (offset < length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var read = stream.Read(array, offset, length - offset);
                if (read == 0)
                    throw new SearchSourceChangedException(path);

                offset = checked(offset + read);
            }

            if (stream.ReadByte() >= 0)
                throw new SearchSourceChangedException(path);

            return new SearchSmallFileBuffer(array, length, creditedBytes);
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(array);
            SearchMemoryCreditGate.Release(creditedBytes);
            throw;
        }
    }

    public void Dispose()
    {
        var array = Interlocked.Exchange(ref _array, null);
        if (array is null)
            return;

        ArrayPool<byte>.Shared.Return(array);
        SearchMemoryCreditGate.Release(_creditedBytes);
    }
}
