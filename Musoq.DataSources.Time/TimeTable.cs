using System;
using System.Linq;
using Musoq.Schema;

namespace Musoq.DataSources.Time;

internal class TimeTable : ISchemaTable
{
    public ISchemaColumn[] Columns { get; } = TimeHelper.TimeColumns;

    public SchemaTableMetadata Metadata { get; } = new(typeof(DateTimeOffset));

    public ISchemaColumn GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}