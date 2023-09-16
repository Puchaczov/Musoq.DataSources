using Musoq.Schema;

namespace Musoq.DataSources.Kubernetes.Events;

internal class EventsTable : ISchemaTable
{
    public ISchemaColumn[] Columns => EventsSourceHelper.EventsColumns;

    public SchemaTableMetadata Metadata { get; } = new(typeof(EventEntity));

    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}