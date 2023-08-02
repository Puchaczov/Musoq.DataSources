using Musoq.Schema;

namespace Musoq.DataSources.Kubernetes.PersistentVolumeClaims;

internal class PersistentVolumeClaimsTable : ISchemaTable
{
    public ISchemaColumn GetColumnByName(string name)
    {
        return Columns.Single(column => column.ColumnName == name);
    }

    public ISchemaColumn[] Columns => PersistentVolumeClaimsSourceHelper.PersistentVolumeClaimsColumns;
    
    public SchemaTableMetadata Metadata { get; } = new(typeof(PersistentVolumeClaimEntity));
}