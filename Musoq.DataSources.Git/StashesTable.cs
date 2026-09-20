using System.Linq;
using Musoq.DataSources.Git.Entities;
using Musoq.Schema;

namespace Musoq.DataSources.Git;

internal sealed class StashesTable : ISchemaTable
{
    public ISchemaColumn[] Columns => StashEntity.Columns;

    public SchemaTableMetadata Metadata => new(typeof(StashEntity));

    public ISchemaColumn? GetColumnByName(string name) =>
        Columns.SingleOrDefault(column => column.ColumnName == name);

    public ISchemaColumn[] GetColumnsByName(string name) =>
        Columns.Where(column => column.ColumnName == name).ToArray();
}
