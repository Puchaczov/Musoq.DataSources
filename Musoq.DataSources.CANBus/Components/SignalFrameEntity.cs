using System;
using System.Collections;
using System.Collections.Generic;
using DbcParserLib;
using DbcParserLib.Model;
using Musoq.Plugins.Attributes;

namespace Musoq.DataSources.CANBus.Components;

/// <summary>Exposes decoded signal values through a concrete nullable-double contract.</summary>
[DynamicObjectPropertyTypeHint("RawData", typeof(ulong?))]
[DynamicObjectPropertyDefaultTypeHint(typeof(double?))]
public sealed class SignalFrameEntity(ulong rawData, Message? message) : IReadOnlyDictionary<string, double?>
{
    /// <summary>The numeric payload used by the DBC signal decoder.</summary>
    public ulong? RawData { get; } = rawData;
    /// <inheritdoc />
    public int Count => message?.Signals.Count ?? 0;
    /// <inheritdoc />
    public IEnumerable<string> Keys
    {
        get
        {
            if (message is null) yield break;
            foreach (var signal in message.Signals) yield return signal.Name;
        }
    }
    /// <inheritdoc />
    public IEnumerable<double?> Values
    {
        get { foreach (var key in Keys) yield return this[key]; }
    }
    /// <inheritdoc />
    public double? this[string key] => TryGetValue(key, out var value) ? value : null;
    /// <inheritdoc />
    public bool ContainsKey(string key)
    {
        if (message is null) return false;
        foreach (var signal in message.Signals)
            if (string.Equals(signal.Name, key, StringComparison.Ordinal)) return true;
        return false;
    }
    /// <inheritdoc />
    public bool TryGetValue(string key, out double? value)
    {
        if (message is not null)
            foreach (var signal in message.Signals)
                if (string.Equals(signal.Name, key, StringComparison.Ordinal))
                {
                    value = Packer.RxSignalUnpack(RawData.GetValueOrDefault(), signal);
                    return true;
                }
        value = null;
        return false;
    }
    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, double?>> GetEnumerator()
    {
        foreach (var key in Keys) yield return new(key, this[key]);
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
