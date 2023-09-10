using Docker.DotNet.Models;
using Musoq.Schema;

namespace Musoq.DataSources.Docker.Volumes;

internal class VolumesTable : ISchemaTable
{
    public ISchemaColumn[] Columns => VolumesSourceHelper.VolumesColumns;
    
    public SchemaTableMetadata Metadata { get; } = new(typeof(VolumeResponse));
    
    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}