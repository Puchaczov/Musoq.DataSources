using Musoq.Schema;

namespace Musoq.DataSources.Kubernetes.CronJobs;

internal class CronJobsTable : ISchemaTable
{
    public ISchemaColumn[] Columns => CronJobsSourceHelper.CronJobsColumns;

    public SchemaTableMetadata Metadata { get; } = new(typeof(CronJobEntity));
    
    public ISchemaColumn GetColumnByName(string name)
    {
        return Columns.Single(column => column.ColumnName == name);
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}