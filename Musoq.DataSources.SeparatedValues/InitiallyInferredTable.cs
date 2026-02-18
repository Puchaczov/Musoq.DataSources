using System.Collections.Generic;
using System.Linq;
using Musoq.Schema;

namespace Musoq.DataSources.SeparatedValues;

internal class InitiallyInferredTable(IReadOnlyCollection<ISchemaColumn> columns) : ISchemaTable
{
    public ISchemaColumn[] Columns { get; } = columns.ToArray();

    public SchemaTableMetadata Metadata => new(typeof(object));

    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}