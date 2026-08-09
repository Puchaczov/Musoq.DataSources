#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;

namespace Musoq.DataSources.Structured;

internal sealed class RepeatedValueChunk<T> : IReadOnlyList<T>
{
    private readonly T _value;

    public RepeatedValueChunk(T value, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        _value = value;
        Count = count;
    }

    public int Count { get; }

    public T this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);
            return _value;
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        return new Enumerator(_value, Count);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private sealed class Enumerator(T value, int count) : IEnumerator<T>
    {
        private int _remaining = count;

        public T Current => value;

        object? IEnumerator.Current => Current;

        public bool MoveNext()
        {
            if (_remaining == 0)
                return false;

            _remaining--;
            return true;
        }

        public void Reset()
        {
            throw new NotSupportedException();
        }

        public void Dispose()
        {
        }
    }
}
