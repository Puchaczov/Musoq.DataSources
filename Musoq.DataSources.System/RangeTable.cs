using System.Linq;
using Musoq.Schema;

namespace Musoq.DataSources.System;

internal class RangeTable : ISchemaTable
{
    public ISchemaColumn[] Columns => RangeHelper.RangeColumns;

    public SchemaTableMetadata Metadata { get; } = new(typeof(RangeItemEntity));

    public ISchemaColumn GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}