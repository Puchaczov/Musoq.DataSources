#nullable enable

using System;
using System.Text;
using System.Text.Json;
using Musoq.DataSources.Structured;

namespace Musoq.DataSources.Json;

internal sealed class JsonPropertyLookup
{
    private readonly ulong[] _hashes;
    private readonly byte[][] _names;
    private readonly int[] _slots;

    public JsonPropertyLookup(string[] names)
    {
        _names = new byte[names.Length][];
        var capacity = 4;
        while (capacity < names.Length * 2)
            capacity <<= 1;

        _hashes = new ulong[capacity];
        _slots = new int[capacity];

        for (var index = 0; index < names.Length; index++)
        {
            var utf8Name = Encoding.UTF8.GetBytes(names[index]);
            _names[index] = utf8Name;
            Insert(Hash(utf8Name), index);
        }
    }

    public int Find(ref Utf8JsonReader reader)
    {
        if (reader.ValueIsEscaped)
        {
            for (var index = 0; index < _names.Length; index++)
                if (reader.ValueTextEquals(_names[index]))
                    return index;

            return -1;
        }

        var name = reader.ValueSpan;
        var hash = Hash(name);
        var mask = _slots.Length - 1;
        var slot = (int)hash & mask;

        while (_slots[slot] != 0)
        {
            var index = _slots[slot] - 1;
            if (_hashes[slot] == hash && name.SequenceEqual(_names[index]))
                return index;
            slot = (slot + 1) & mask;
        }

        return -1;
    }

    public static ulong Hash(ReadOnlySpan<byte> value)
    {
        return StructuredUtf8Hash.Hash(value);
    }

    private void Insert(ulong hash, int index)
    {
        var mask = _slots.Length - 1;
        var slot = (int)hash & mask;
        while (_slots[slot] != 0)
            slot = (slot + 1) & mask;

        _hashes[slot] = hash;
        _slots[slot] = index + 1;
    }
}
