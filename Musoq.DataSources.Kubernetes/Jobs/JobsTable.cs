using Musoq.Schema;

namespace Musoq.DataSources.Kubernetes.Jobs;

internal class JobsTable : ISchemaTable
{
    public ISchemaColumn GetColumnByName(string name)
    {
        return Columns.Single(column => column.ColumnName == name);
    }

    public ISchemaColumn[] Columns => JobsSourceHelper.JobsColumns;
    
    public SchemaTableMetadata Metadata { get; } = new(typeof(JobEntity));
}