using Musoq.DataSources.Jira.Entities;
using Musoq.Schema;

namespace Musoq.DataSources.Jira.Sources.Projects;

internal class ProjectsTable : ISchemaTable
{
    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }

    public ISchemaColumn[] Columns => ProjectsSourceHelper.ProjectsColumns;

    public SchemaTableMetadata Metadata { get; } = new(typeof(IJiraProject));
}