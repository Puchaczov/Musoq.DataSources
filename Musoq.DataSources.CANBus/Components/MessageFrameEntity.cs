﻿using System;
using System.Collections.Generic;
using System.Dynamic;
using DbcParserLib.Model;

namespace Musoq.DataSources.CANBus.Components;

/// <summary>
/// Represents a message frame. It is a dynamic object that allows to access signals as properties.
/// </summary>
public class MessageFrameEntity : DynamicObject, ICANDbcMessage
{
    private const string Timestamp = nameof(Timestamp);

    private readonly Dictionary<string, Func<object?>> _memberToValueMap;
    private readonly HashSet<string> _allMessagesSet;

    /// <summary>
    /// Creates a new instance of <see cref="MessageFrameEntity"/> class.
    /// </summary>
    /// <param name="timestamp">Timestamp of the frame.</param>
    /// <param name="frame">CAN frame.</param>
    /// <param name="message">Message.</param>
    /// <param name="allMessagesSet">Set of all messages.</param>
    public MessageFrameEntity(ulong timestamp, CANFrame frame, Message? message, HashSet<string> allMessagesSet)
    {
        _allMessagesSet = allMessagesSet;
        
        var uint64Value = ConvertToUInt64(frame.Data);
        
        _memberToValueMap = new Dictionary<string, Func<object?>>
        {
            { "ID", () => frame.Id },
            { nameof(Timestamp), () => timestamp },
            { nameof(Message), () => message },
            { "IsWellKnown", () => message is not null },
            { "DataAsBytes", () => frame.Data },
            { "Data", () => uint64Value }
        };
        var expandoObject = new SignalFrameEntity(uint64Value, message);

        if (message is null)
        {
            _memberToValueMap.Add("UnknownMessage", () => expandoObject);
            return;
        }
        
        _memberToValueMap.Add(message.Name, () => expandoObject);
    }
    
    /// <summary>
    /// Gets the message.
    /// </summary>
    public Message? Message => (Message?)_memberToValueMap[nameof(Message)]();

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
    /// Creates a map of message names to their indexes.
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
    /// Creates a map of message indexes to their access methods.
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