using System.Linq;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.System;

internal class DualTable : ISchemaTable
{
    public ISchemaColumn[] Columns =>
    [
        new SchemaColumn(nameof(DualEntity.Dummy), 0, typeof(string))
    ];

    public SchemaTableMetadata Metadata { get; } = new(typeof(DualEntity));

    public ISchemaColumn GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}