using System.Linq;
using Musoq.DataSources.Git.Entities;
using Musoq.Schema;

namespace Musoq.DataSources.Git;

internal sealed class WorktreesTable : ISchemaTable
{
    public ISchemaColumn[] Columns => WorktreeEntity.Columns;
    public SchemaTableMetadata Metadata => new(typeof(WorktreeEntity));
    public ISchemaColumn? GetColumnByName(string name) => Columns.SingleOrDefault(c => c.ColumnName == name);
    public ISchemaColumn[] GetColumnsByName(string name) => Columns.Where(c => c.ColumnName == name).ToArray();
}
