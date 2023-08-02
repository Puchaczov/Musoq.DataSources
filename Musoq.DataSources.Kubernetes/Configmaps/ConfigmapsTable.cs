using Musoq.Schema;

namespace Musoq.DataSources.Kubernetes.Configmaps;

internal class ConfigmapsTable : ISchemaTable
{
    public ISchemaColumn GetColumnByName(string name)
    {
        return Columns.Single(column => column.ColumnName == name);
    }

    public ISchemaColumn[] Columns => ConfigmapsSourceHelper.ConfigmapsColumns;
    
    public SchemaTableMetadata Metadata { get; } = new(typeof(ConfigmapEntity));
}