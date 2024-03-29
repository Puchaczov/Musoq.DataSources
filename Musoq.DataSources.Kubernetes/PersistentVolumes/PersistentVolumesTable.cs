﻿using Musoq.Schema;

namespace Musoq.DataSources.Kubernetes.PersistentVolumes;

internal class PersistentVolumesTable : ISchemaTable
{
    public ISchemaColumn[] Columns => PersistentVolumesSourceHelper.PersistentVolumesColumns;
    
    public SchemaTableMetadata Metadata { get; } = new(typeof(PersistentVolumeEntity));
    
    public ISchemaColumn GetColumnByName(string name)
    {
        return Columns.Single(column => column.ColumnName == name);
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}