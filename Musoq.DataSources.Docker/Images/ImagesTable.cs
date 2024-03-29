﻿using Docker.DotNet.Models;
using Musoq.Schema;

namespace Musoq.DataSources.Docker.Images;

internal class ImagesTable : ISchemaTable
{
    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }

    public ISchemaColumn[] Columns => ImagesSourceHelper.ImagesColumns;
    
    public SchemaTableMetadata Metadata { get; } = new(typeof(ImagesListResponse));
}