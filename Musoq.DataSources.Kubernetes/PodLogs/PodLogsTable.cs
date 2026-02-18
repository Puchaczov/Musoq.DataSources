using Musoq.Schema;

namespace Musoq.DataSources.Kubernetes.PodLogs;

internal class PodLogsTable : ISchemaTable
{
    public ISchemaColumn[] Columns => PodLogsSourceHelper.PodLogsColumns;

    public SchemaTableMetadata Metadata { get; } = new(typeof(PodLogsEntity));

    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}