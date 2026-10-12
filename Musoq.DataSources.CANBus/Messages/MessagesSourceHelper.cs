using System;
using System.Collections.Generic;
using Musoq.DataSources.CANBus.Signals;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.CANBus.Messages;

internal static class MessagesSourceHelper
{
    internal static readonly IReadOnlyDictionary<string, int> MessagesNameToIndexMap = new Dictionary<string, int>
    {
        { nameof(MessageEntity.Id), 0 },
        { nameof(MessageEntity.IsExtId), 1 },
        { nameof(MessageEntity.Name), 2 },
        { nameof(MessageEntity.DLC), 3 },
        { nameof(MessageEntity.Transmitter), 4 },
        { nameof(MessageEntity.Comment), 5 },
        { nameof(MessageEntity.CycleTime), 6 },
        { nameof(MessageEntity.Signals), 7 }
    };

    internal static ISchemaColumn[] Columns =>
    [
        new SchemaColumn(nameof(MessageEntity.Id), 0, typeof(uint)),
        new SchemaColumn(nameof(MessageEntity.IsExtId), 1, typeof(bool)),
        new SchemaColumn(nameof(MessageEntity.Name), 2, typeof(string)),
        new SchemaColumn(nameof(MessageEntity.DLC), 3, typeof(ushort)),
        new SchemaColumn(nameof(MessageEntity.Transmitter), 4, typeof(string)),
        new SchemaColumn(nameof(MessageEntity.Comment), 5, typeof(string)),
        new SchemaColumn(nameof(MessageEntity.CycleTime), 6, typeof(int)),
        new SchemaColumn(nameof(MessageEntity.Signals), 7, typeof(IEnumerable<SignalEntity>))
    ];
}