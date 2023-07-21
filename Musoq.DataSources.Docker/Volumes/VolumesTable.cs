﻿using Musoq.Schema;

namespace Musoq.DataSources.Docker.Volumes;

public class VolumesTable : ISchemaTable
{
    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] Columns => VolumesSourceHelper.VolumesColumns;
}