using System.Collections;
using System.Collections.Concurrent;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.AsyncRowsSource;

internal class ChunkEnumerator(
    BlockingCollection<IReadOnlyList<IObjectResolver>> readRows,
    Func<Exception?> getException,
    CancellationToken token)
    : IEnumerator<IObjectResolver>
{
    private IEnumerator<IObjectResolver>? _currentChunkEnumerator;

    public bool MoveNext()
    {
        getException()?.Let(exc => throw exc);

        while (true)
        {
            if (_currentChunkEnumerator != null)
                if (_currentChunkEnumerator.MoveNext())
                    return true;

            if (readRows.TryTake(out var chunk, 100))
            {
                if (chunk.Count == 0)
                    continue;
                _currentChunkEnumerator = chunk.GetEnumerator();
            }
            else if (readRows.IsCompleted || readRows.Count == 0)
            {
                var exception = getException();
                if (exception != null)
                    throw exception;

                if (token.IsCancellationRequested)
                    return false;
                if (readRows.IsCompleted)
                    return false;
            }
        }
    }

    public IObjectResolver Current
    {
        get
        {
            if (_currentChunkEnumerator == null)
                throw new InvalidOperationException("Enumeration has not started.");
            return _currentChunkEnumerator.Current;
        }
    }

    object IEnumerator.Current => Current;

    public void Reset()
    {
        throw new NotSupportedException("Chunk enumerator does not support reset.");
    }

    public void Dispose()
    {
        _currentChunkEnumerator?.Dispose();
    }
}

internal static class Extensions
{
    public static void Let<T>(this T? value, Action<T> action)
    {
        if (value is not null)
            action(value);
    }
}