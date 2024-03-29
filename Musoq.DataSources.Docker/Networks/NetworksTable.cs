﻿using Docker.DotNet.Models;
using Musoq.Schema;

namespace Musoq.DataSources.Docker.Networks;

internal class NetworksTable : ISchemaTable
{
    public ISchemaColumn[] Columns => NetworksSourceHelper.NetworksColumns;
    
    public SchemaTableMetadata Metadata { get; } = new(typeof(NetworkResponse));
    
    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}