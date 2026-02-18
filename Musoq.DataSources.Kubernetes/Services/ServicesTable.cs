using Musoq.Schema;

namespace Musoq.DataSources.Kubernetes.Services;

internal class ServicesTable : ISchemaTable
{
    public ISchemaColumn[] Columns => ServicesSourceHelper.ServicesColumns;

    public SchemaTableMetadata Metadata { get; } = new(typeof(ServiceEntity));

    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}