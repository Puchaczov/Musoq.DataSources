using System.Collections.Generic;
using System.Linq;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.SeparatedValues;

internal class InitiallyInferredTable(IReadOnlyCollection<ISchemaColumn> columns) : ISchemaTable
{
    public ISchemaColumn[] Columns { get; } = columns.Select(NormalizeColumn).ToArray();

    public SchemaTableMetadata Metadata => new(typeof(object[]));

    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }

    private static ISchemaColumn NormalizeColumn(ISchemaColumn column)
    {
        return column.ColumnType == typeof(object)
            ? new SchemaColumn(column.ColumnName, column.ColumnIndex, typeof(string))
            : column;
    }
}
