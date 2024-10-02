using System.Linq;
using Musoq.DataSources.Roslyn.Entities;
using Musoq.Schema;

namespace Musoq.DataSources.Roslyn;

internal class SolutionTable : ISchemaTable
{
    public ISchemaColumn[] Columns => SolutionEntity.Columns;

    public SchemaTableMetadata Metadata => new(typeof(SolutionEntity));

    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}