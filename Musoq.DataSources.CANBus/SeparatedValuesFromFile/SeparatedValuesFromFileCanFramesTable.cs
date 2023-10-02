using System.Collections.Generic;
using System.Linq;
using DbcParserLib.Model;
using Musoq.DataSources.CANBus.Components;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.CANBus.SeparatedValuesFromFile;

internal class SeparatedValuesFromFileCanFramesTable : ISchemaTable
{
    private readonly ICANBusApi _canBusApi;
    private ISchemaColumn[]? _columns;

    public SeparatedValuesFromFileCanFramesTable(ICANBusApi canBusApi)
    {
        _canBusApi = canBusApi;
        _columns = null;
    }

    public ISchemaColumn[] Columns
    {
        get
        {
            if (_columns != null)
                return _columns;
            
            var columnsDictionary = new Dictionary<string, ISchemaColumn>
            {
                {"ID", new SchemaColumn("ID", 0, typeof(uint))},
                {"Timestamp", new SchemaColumn("Timestamp", 0, typeof(ulong))},
                {nameof(Message), new SchemaColumn(nameof(Message), 1, typeof(Message))},
                {"IsWellKnown", new SchemaColumn("IsWellKnown", 2, typeof(bool))},
                {"UnknownMessage", new SchemaColumn("UnknownMessage", 3, typeof(SignalFrameEntity))}
            };

            foreach (var message in _canBusApi.GetMessages())
            {
                columnsDictionary.Add(message.Name, new SchemaColumn(message.Name, columnsDictionary.Count, typeof(SignalFrameEntity)));
            }
            
            _columns = columnsDictionary.Values.ToArray();
            return _columns;
        }
    }
    
    public SchemaTableMetadata Metadata => new(typeof(MessageFrameEntity));
    
    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.FirstOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}