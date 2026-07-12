using System.Linq;
using Musoq.Schema;

namespace Musoq.DataSources.Os.Runtime;

internal abstract class RuntimeDiscoveryTableBase<TEntity>(ISchemaColumn[] columns) : ISchemaTable
{
    public ISchemaColumn[] Columns { get; } = columns;

    public SchemaTableMetadata Metadata { get; } = new(typeof(TEntity));

    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}
