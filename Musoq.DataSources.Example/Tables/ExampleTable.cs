using Musoq.Schema;
using Musoq.DataSources.Example.Entities;

namespace Musoq.DataSources.Example.Tables;

internal class ExampleTable : ISchemaTable
{
    public ISchemaColumn[] Columns => ExampleTableHelper.Columns;
    
    public SchemaTableMetadata Metadata { get; } = new(typeof(ExampleEntity));

    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }
    
    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}