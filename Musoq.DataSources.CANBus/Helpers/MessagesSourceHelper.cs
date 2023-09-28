using System;
using System.Collections.Generic;
using DbcParserLib.Model;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.CANBus.Helpers;

internal static class MessagesSourceHelper
{
    internal static readonly IDictionary<string, int> MessagesNameToIndexMap = new Dictionary<string, int>
    {
        { nameof(Message.ID), 0 },
        { nameof(Message.IsExtID), 1 },
        { nameof(Message.Name), 2 },
        { nameof(Message.DLC), 3 },
        { nameof(Message.Transmitter), 4 },
        { nameof(Message.Comment), 5 },
        { nameof(Message.CycleTime), 6 },
    };

    internal static readonly IDictionary<int, Func<Message, object>> MessagesIndexToMethodAccessMap = new Dictionary<int, Func<Message, object>>
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
        new SchemaColumn(nameof(Message.ID), 0, typeof(uint)),
        new SchemaColumn(nameof(Message.IsExtID), 1, typeof(bool)),
        new SchemaColumn(nameof(Message.Name), 2, typeof(string)),
        new SchemaColumn(nameof(Message.DLC), 3, typeof(uint)),
        new SchemaColumn(nameof(Message.Transmitter), 4, typeof(string)),
        new SchemaColumn(nameof(Message.Comment), 5, typeof(string)),
        new SchemaColumn(nameof(Message.CycleTime), 6, typeof(uint)),
    };
}