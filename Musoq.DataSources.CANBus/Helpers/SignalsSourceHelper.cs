using System;
using System.Collections.Generic;
using DbcParserLib.Model;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.CANBus.Helpers;

internal static class SignalsSourceHelper
{
    internal static readonly IDictionary<string, int> SignalsNameToIndexMap = new Dictionary<string, int>
    {
        { nameof(Signal.ID), 0 },
        { nameof(Signal.Name), 1 },
        { nameof(Signal.StartBit), 2 },
        { nameof(Signal.Length), 3 },
        { nameof(Signal.ByteOrder), 4 },
        { nameof(Signal.InitialValue), 5 },
        { nameof(Signal.Factor), 6 },
        { nameof(Signal.IsInteger), 7 },
        { nameof(Signal.Offset), 8 },
        { nameof(Signal.Minimum), 9 },
        { nameof(Signal.Maximum), 10 },
        { nameof(Signal.Unit), 11 },
        { nameof(Signal.Receiver), 12 },
        { nameof(Signal.Comment), 13 },
        { nameof(Signal.Multiplexing), 14 },
        { nameof(Signal.ValueType), 15 },
        { nameof(Signal.ValueTableMap), 16 },
    };

    internal static readonly IDictionary<int, Func<Signal, object>> SignalsIndexToMethodAccessMap = new Dictionary<int, Func<Signal, object>>
    {
        { 0, f => f.ID },
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
        { 15, f => f.ValueType },
        { 16, f => f.ValueTableMap },
    };

    internal static ISchemaColumn[] Columns => new ISchemaColumn[]
    {
        new SchemaColumn(nameof(Signal.ID), 0, typeof(uint)),
        new SchemaColumn(nameof(Signal.Name), 1, typeof(string)),
        new SchemaColumn(nameof(Signal.StartBit), 2, typeof(uint)),
        new SchemaColumn(nameof(Signal.Length), 3, typeof(uint)),
        new SchemaColumn(nameof(Signal.ByteOrder), 4, typeof(string)),
        new SchemaColumn(nameof(Signal.InitialValue), 5, typeof(string)),
        new SchemaColumn(nameof(Signal.Factor), 6, typeof(double)),
        new SchemaColumn(nameof(Signal.IsInteger), 7, typeof(bool)),
        new SchemaColumn(nameof(Signal.Offset), 8, typeof(double)),
        new SchemaColumn(nameof(Signal.Minimum), 9, typeof(double)),
        new SchemaColumn(nameof(Signal.Maximum), 10, typeof(double)),
        new SchemaColumn(nameof(Signal.Unit), 11, typeof(string)),
        new SchemaColumn(nameof(Signal.Receiver), 12, typeof(string)),
        new SchemaColumn(nameof(Signal.Comment), 13, typeof(string)),
        new SchemaColumn(nameof(Signal.Multiplexing), 14, typeof(string)),
        new SchemaColumn(nameof(Signal.ValueType), 15, typeof(string)),
        new SchemaColumn(nameof(Signal.ValueTableMap), 16, typeof(string))
    };
}