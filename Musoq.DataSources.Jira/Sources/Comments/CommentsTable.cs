using Musoq.DataSources.Jira.Entities;
using Musoq.Schema;

namespace Musoq.DataSources.Jira.Sources.Comments;

internal class CommentsTable : ISchemaTable
{
    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }

    public ISchemaColumn[] Columns => CommentsSourceHelper.CommentsColumns;

    public SchemaTableMetadata Metadata { get; } = new(typeof(IJiraComment));
}