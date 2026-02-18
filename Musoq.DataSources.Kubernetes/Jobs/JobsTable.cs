using Musoq.Schema;

namespace Musoq.DataSources.Kubernetes.Jobs;

internal class JobsTable : ISchemaTable
{
    public ISchemaColumn[] Columns => JobsSourceHelper.JobsColumns;

    public SchemaTableMetadata Metadata { get; } = new(typeof(JobEntity));

    public ISchemaColumn GetColumnByName(string name)
    {
        return Columns.Single(column => column.ColumnName == name);
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}