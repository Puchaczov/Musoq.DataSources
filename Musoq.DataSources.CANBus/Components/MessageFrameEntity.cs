using System;
using System.Collections;
using System.Collections.Generic;
using DbcParserLib.Model;

namespace Musoq.DataSources.CANBus.Components;

/// <summary>Retains native frame fields and exposes message payloads through typed dictionary lookup.</summary>
public sealed class MessageFrameEntity : ICANDbcMessage, IReadOnlyDictionary<string, SignalFrameEntity?>
{
    private readonly HashSet<string> _allMessages;
    private readonly string _messageName;
    private SignalFrameEntity? _signals;
    private ulong? _rawData;

    /// <summary>Creates a frame with its authoritative DBC metadata and native payload.</summary>
    public MessageFrameEntity(ulong timestamp, CANFrame frame, Message? message, HashSet<string> allMessagesSet)
    {
        Timestamp = timestamp;
        ID = frame.Id;
        DataAsBytes = frame.Data;
        Message = message;
        _allMessages = allMessagesSet;
        _messageName = message?.Name ?? "UnknownMessage";
    }

    /// <summary>The frame identifier.</summary>
    public uint ID { get; }
    /// <summary>The source timestamp.</summary>
    public ulong Timestamp { get; }
    /// <summary>The matching DBC definition, or null for an unknown frame.</summary>
    public Message? Message { get; }
    /// <summary>Whether the frame identifier has a DBC definition.</summary>
    public bool IsWellKnown => Message is not null;
    /// <summary>The payload bytes in source-defined byte order.</summary>
    public byte[] DataAsBytes { get; }
    /// <summary>The little-endian numeric payload, decoded once per frame.</summary>
    public ulong Data
    {
        get
        {
            if (_rawData.HasValue) return _rawData.Value;
            Span<byte> bytes = stackalloc byte[8];
            bytes.Clear();
            DataAsBytes.CopyTo(bytes);
            _rawData = BitConverter.ToUInt64(bytes);
            return _rawData.Value;
        }
    }

    /// <inheritdoc />
    public int Count => _allMessages.Count + (_allMessages.Contains("UnknownMessage") ? 0 : 1);
    /// <inheritdoc />
    public IEnumerable<string> Keys
    {
        get
        {
            foreach (var name in _allMessages) yield return name;
            if (!_allMessages.Contains("UnknownMessage")) yield return "UnknownMessage";
        }
    }
    /// <inheritdoc />
    public IEnumerable<SignalFrameEntity?> Values
    {
        get { foreach (var key in Keys) yield return this[key]; }
    }
    /// <inheritdoc />
    public SignalFrameEntity? this[string key] => TryGetValue(key, out var value) ? value : null;
    /// <inheritdoc />
    public bool ContainsKey(string key) => key == "UnknownMessage" || _allMessages.Contains(key);
    /// <inheritdoc />
    public bool TryGetValue(string key, out SignalFrameEntity? value)
    {
        if (string.Equals(key, _messageName, StringComparison.Ordinal))
        {
            value = _signals ??= new SignalFrameEntity(Data, Message);
            return true;
        }
        value = null;
        return ContainsKey(key);
    }
    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, SignalFrameEntity?>> GetEnumerator()
    {
        foreach (var key in Keys) yield return new(key, this[key]);
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
