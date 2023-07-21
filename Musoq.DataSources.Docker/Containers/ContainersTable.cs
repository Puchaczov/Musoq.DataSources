﻿using Musoq.Schema;

namespace Musoq.DataSources.Docker.Containers;

internal class ContainersTable : ISchemaTable
{
    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] Columns => ContainersSourceHelper.ContainersColumns;
}