using System.Linq;
using Musoq.DataSources.Git.Entities;
using Musoq.Schema;

namespace Musoq.DataSources.Git;

internal sealed class RemoteTagsTable : ISchemaTable
{
    public ISchemaColumn[] Columns => RemoteTagEntity.Columns;

    public SchemaTableMetadata Metadata => new(typeof(RemoteTagEntity));

    public ISchemaColumn? GetColumnByName(string name) =>
        Columns.SingleOrDefault(column => column.ColumnName == name);

    public ISchemaColumn[] GetColumnsByName(string name) =>
        Columns.Where(column => column.ColumnName == name).ToArray();
}
