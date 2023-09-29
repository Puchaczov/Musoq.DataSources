using System;
using System.Collections.Generic;
using Musoq.DataSources.CANBus.Messages;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.CANBus.Helpers;

internal static class MessagesSourceHelper
{
    internal static readonly IDictionary<string, int> MessagesNameToIndexMap = new Dictionary<string, int>
    {
        { nameof(MessageEntity.ID), 0 },
        { nameof(MessageEntity.IsExtID), 1 },
        { nameof(MessageEntity.Name), 2 },
        { nameof(MessageEntity.DLC), 3 },
        { nameof(MessageEntity.Transmitter), 4 },
        { nameof(MessageEntity.Comment), 5 },
        { nameof(MessageEntity.CycleTime), 6 },
    };

    internal static readonly IDictionary<int, Func<MessageEntity, object>> MessagesIndexToMethodAccessMap = new Dictionary<int, Func<MessageEntity, object>>
    {
        { 0, f => f.ID },
        { 1, f => f.IsExtID },
        { 2, f => f.Name },
        { 3, f => f.DLC },
        { 4, f => f.Transmitter },
        { 5, f => f.Comment },
        { 6, f => f.CycleTime },
    };
    
    internal static ISchemaColumn[] Columns => new ISchemaColumn[]
    {
        new SchemaColumn(nameof(MessageEntity.ID), 0, typeof(uint)),
        new SchemaColumn(nameof(MessageEntity.IsExtID), 1, typeof(bool)),
        new SchemaColumn(nameof(MessageEntity.Name), 2, typeof(string)),
        new SchemaColumn(nameof(MessageEntity.DLC), 3, typeof(uint)),
        new SchemaColumn(nameof(MessageEntity.Transmitter), 4, typeof(string)),
        new SchemaColumn(nameof(MessageEntity.Comment), 5, typeof(string)),
        new SchemaColumn(nameof(MessageEntity.CycleTime), 6, typeof(uint)),
    };
}