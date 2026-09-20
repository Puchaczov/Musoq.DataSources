#nullable enable

using System;
using System.Buffers;
using System.Threading;

namespace Musoq.DataSources.Search.Components.Text;

internal sealed class SearchCharBuffer : IDisposable
{
    internal const int RequestedLength = 8192;

    private char[]? _array;

    private SearchCharBuffer(char[] array)
    {
        _array = array;
    }

    internal char[] Array =>
        Volatile.Read(ref _array) ?? throw new ObjectDisposedException(nameof(SearchCharBuffer));

    internal int Length => RequestedLength;

    internal bool IsReturned => Volatile.Read(ref _array) is null;

    internal static SearchCharBuffer Rent()
    {
        return new SearchCharBuffer(ArrayPool<char>.Shared.Rent(RequestedLength));
    }

    public void Dispose()
    {
        var array = Interlocked.Exchange(ref _array, null);
        if (array is not null)
            ArrayPool<char>.Shared.Return(array);
    }
}
