using System;
using System.Collections.Generic;
using System.Dynamic;
using DbcParserLib.Model;

namespace Musoq.DataSources.CANBus.Components;

/// <summary>
///     Represents a message frame. It is a dynamic object that allows to access signals as properties.
/// </summary>
public class MessageFrameEntity : DynamicObject, ICANDbcMessage
{
    private const string Timestamp = nameof(Timestamp);
    private readonly HashSet<string> _allMessagesSet;
    private readonly Message? _message;

    private readonly Dictionary<string, Func<object?>> _memberToValueMap;

    /// <summary>
    ///     Creates a new instance of <see cref="MessageFrameEntity" /> class.
    /// </summary>
    /// <param name="timestamp">Timestamp of the frame.</param>
    /// <param name="frame">CAN frame.</param>
    /// <param name="message">Message.</param>
    /// <param name="allMessagesSet">Set of all messages.</param>
    /// <param name="requestedColumns">Projected column names, or null when all members are required.</param>
    public MessageFrameEntity(
        ulong timestamp,
        CANFrame frame,
        Message? message,
        HashSet<string> allMessagesSet,
        IReadOnlySet<string>? requestedColumns = null)
    {
        _allMessagesSet = allMessagesSet;
        _message = message;

        ulong? uint64Value = null;
        ulong GetRawData()
        {
            uint64Value ??= ConvertToUInt64(frame.Data);
            return uint64Value.Value;
        }

        _memberToValueMap = new Dictionary<string, Func<object?>>();

        AddIfRequested("ID", () => frame.Id);
        AddIfRequested(nameof(Timestamp), () => timestamp);
        _memberToValueMap.Add(nameof(Message), () => _message);
        AddIfRequested("IsWellKnown", () => message is not null);
        AddIfRequested("DataAsBytes", () => frame.Data);
        AddIfRequested("Data", () => GetRawData());

        var dynamicMessageName = message?.Name ?? "UnknownMessage";
        _memberToValueMap.Add(dynamicMessageName, () => new SignalFrameEntity(GetRawData(), message));

        void AddIfRequested(string name, Func<object?> value)
        {
            if (requestedColumns is null || requestedColumns.Contains(name))
                _memberToValueMap.Add(name, value);
        }
    }

    /// <summary>
    ///     Gets the message.
    /// </summary>
    public Message? Message => _message;

    /// <inheritdoc />
    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        if (_memberToValueMap.TryGetValue(binder.Name, out var value))
        {
            result = value();
            return true;
        }

        if (_allMessagesSet.Contains(binder.Name))
        {
            result = null;
            return true;
        }

        result = null;
        return false;
    }

    /// <summary>
    ///     Creates a map of message names to their indexes.
    /// </summary>
    /// <returns></returns>
    public IReadOnlyDictionary<string, int> CreateMessageNameToIndexMap()
    {
        var index = 0;
        var map = new Dictionary<string, int>();

        foreach (var member in _memberToValueMap)
        {
            map.Add(member.Key, index);
            index += 1;
        }

        return map;
    }

    /// <summary>
    ///     Creates a map of message indexes to their access methods.
    /// </summary>
    /// <returns></returns>
    public IReadOnlyDictionary<int, Func<MessageFrameEntity, object?>> CreateMessageIndexToMethodAccessMap()
    {
        var index = 0;
        var map = new Dictionary<int, Func<MessageFrameEntity, object?>>();

        foreach (var member in _memberToValueMap)
        {
            map.Add(index, frame => frame._memberToValueMap[member.Key]());
            index += 1;
        }

        return map;
    }

    private static ulong ConvertToUInt64(byte[] frameData)
    {
        var data = new byte[8];
        Array.Copy(frameData, data, frameData.Length);
        return BitConverter.ToUInt64(data, 0);
    }
}
