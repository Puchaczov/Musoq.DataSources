#nullable enable
using System;
using Musoq.Schema.Diagnostics;

namespace Musoq.DataSources.Structured;

/// <summary>Sparse execution-owned slots, cleared at every record boundary.</summary>
internal sealed class RecordStringCache
{
    private readonly string?[] _values;
    private readonly long[] _positions;
    private bool _reported;
    private bool _hasValues;
    internal RecordStringCache(int slots)
    {
        _values = new string?[slots];
        _positions = new long[slots];
    }
    internal int Slots => _values.Length;
    internal long Decoded { get; private set; }
    internal long Reused { get; private set; }
    internal int Retained
    {
        get { var count = 0; foreach (var value in _values) if (value is not null) count++; return count; }
    }
    internal bool TryRead(int slot, long position, out string value)
    {
        if ((uint)slot < (uint)_values.Length && _positions[slot] == position && _values[slot] is { } found)
        { value = found; Reused++; return true; }
        value = null!;
        return false;
    }
    internal void StoreDecoded(int slot, long position, string value)
    {
        _hasValues = true;
        _values[slot] = value;
        _positions[slot] = position;
        Decoded++;
    }
    internal void Clear()
    {
        if (!_hasValues) return;
        _hasValues = false;
        Array.Clear(_values);
        Array.Fill(_positions, -1L);
    }
    internal void Report(SourceDiagnostics? diagnostics)
    {
        if (_reported || diagnostics is null) return;
        _reported = true;
        diagnostics.AddMetric("Pattern.ReuseSlots", Slots);
        diagnostics.AddMetric("Pattern.DecodedStrings", Decoded);
        diagnostics.AddMetric("Pattern.ReusedStrings", Reused);
    }
}
