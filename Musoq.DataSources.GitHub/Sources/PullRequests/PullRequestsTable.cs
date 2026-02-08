using Musoq.DataSources.GitHub.Entities;
using Musoq.Schema;

namespace Musoq.DataSources.GitHub.Sources.PullRequests;

internal class PullRequestsTable : ISchemaTable
{
    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }

    public ISchemaColumn[] Columns => PullRequestsSourceHelper.PullRequestsColumns;
    
    public SchemaTableMetadata Metadata { get; } = new(typeof(PullRequestEntity));
}
