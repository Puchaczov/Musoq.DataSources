using System;
using System.Collections.Generic;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.CANBus.Signals;

internal static class SignalsSourceHelper
{
    internal static readonly IReadOnlyDictionary<string, int> SignalsNameToIndexMap = new Dictionary<string, int>
    {
        { nameof(SignalEntity.Id), 0 },
        { nameof(SignalEntity.Name), 1 },
        { nameof(SignalEntity.StartBit), 2 },
        { nameof(SignalEntity.Length), 3 },
        { nameof(SignalEntity.ByteOrder), 4 },
        { nameof(SignalEntity.InitialValue), 5 },
        { nameof(SignalEntity.Factor), 6 },
        { nameof(SignalEntity.IsInteger), 7 },
        { nameof(SignalEntity.Offset), 8 },
        { nameof(SignalEntity.Minimum), 9 },
        { nameof(SignalEntity.Maximum), 10 },
        { nameof(SignalEntity.Unit), 11 },
        { nameof(SignalEntity.Receiver), 12 },
        { nameof(SignalEntity.Comment), 13 },
        { nameof(SignalEntity.Multiplexing), 14 },
        { nameof(SignalEntity.MessageName), 15 }
    };

    internal static readonly IReadOnlyDictionary<int, Func<SignalEntity, object>> SignalsIndexToMethodAccessMap = new Dictionary<int, Func<SignalEntity, object>>
    {
        { 0, f => f.Id },
        { 1, f => f.Name },
        { 2, f => f.StartBit },
        { 3, f => f.Length },
        { 4, f => f.ByteOrder },
        { 5, f => f.InitialValue },
        { 6, f => f.Factor },
        { 7, f => f.IsInteger },
        { 8, f => f.Offset },
        { 9, f => f.Minimum },
        { 10, f => f.Maximum },
        { 11, f => f.Unit },
        { 12, f => f.Receiver },
        { 13, f => f.Comment },
        { 14, f => f.Multiplexing },
        { 15, f => f.MessageName }
    };

    internal static ISchemaColumn[] Columns =>
    [
        new SchemaColumn(nameof(SignalEntity.Id), 0, typeof(uint)),
        new SchemaColumn(nameof(SignalEntity.Name), 1, typeof(string)),
        new SchemaColumn(nameof(SignalEntity.StartBit), 2, typeof(ushort)),
        new SchemaColumn(nameof(SignalEntity.Length), 3, typeof(ushort)),
        new SchemaColumn(nameof(SignalEntity.ByteOrder), 4, typeof(byte)),
        new SchemaColumn(nameof(SignalEntity.InitialValue), 5, typeof(double)),
        new SchemaColumn(nameof(SignalEntity.Factor), 6, typeof(double)),
        new SchemaColumn(nameof(SignalEntity.IsInteger), 7, typeof(bool)),
        new SchemaColumn(nameof(SignalEntity.Offset), 8, typeof(double)),
        new SchemaColumn(nameof(SignalEntity.Minimum), 9, typeof(double)),
        new SchemaColumn(nameof(SignalEntity.Maximum), 10, typeof(double)),
        new SchemaColumn(nameof(SignalEntity.Unit), 11, typeof(string)),
        new SchemaColumn(nameof(SignalEntity.Receiver), 12, typeof(string[])),
        new SchemaColumn(nameof(SignalEntity.Comment), 13, typeof(string)),
        new SchemaColumn(nameof(SignalEntity.Multiplexing), 14, typeof(string)),
        new SchemaColumn(nameof(SignalEntity.MessageName), 15, typeof(string))
    ];
}