using System;
using System.Linq;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using SharpCompress.Common;

namespace Musoq.DataSources.Archives;

internal class ArchivesTable : ISchemaTable
{
    public ISchemaColumn[] Columns { get; } =
    [
        new SchemaColumn(nameof(EntryWrapper.CompressionType), 0, typeof(CompressionType)),
        new SchemaColumn(nameof(EntryWrapper.ArchivedTime), 1, typeof(DateTime?)),
        new SchemaColumn(nameof(EntryWrapper.CompressedSize), 2, typeof(long)),
        new SchemaColumn(nameof(EntryWrapper.Crc), 3, typeof(long)),
        new SchemaColumn(nameof(EntryWrapper.CreatedTime), 4, typeof(DateTime?)),
        new SchemaColumn(nameof(EntryWrapper.Key), 5, typeof(string)),
        new SchemaColumn(nameof(EntryWrapper.LinkTarget), 6, typeof(string)),
        new SchemaColumn(nameof(EntryWrapper.IsDirectory), 7, typeof(bool)),
        new SchemaColumn(nameof(EntryWrapper.IsEncrypted), 8, typeof(bool)),
        new SchemaColumn(nameof(EntryWrapper.IsSplitAfter), 9, typeof(bool)),
        new SchemaColumn(nameof(EntryWrapper.IsSolid), 10, typeof(bool)),
        new SchemaColumn(nameof(EntryWrapper.VolumeIndexFirst), 11, typeof(int)),
        new SchemaColumn(nameof(EntryWrapper.VolumeIndexLast), 12, typeof(int)),
        new SchemaColumn(nameof(EntryWrapper.LastAccessedTime), 13, typeof(DateTime?)),
        new SchemaColumn(nameof(EntryWrapper.LastModifiedTime), 14, typeof(DateTime?)),
        new SchemaColumn(nameof(EntryWrapper.Size), 15, typeof(long)),
        new SchemaColumn(nameof(EntryWrapper.Attrib), 16, typeof(int?))
    ];

    public SchemaTableMetadata Metadata { get; } = new(typeof(EntryWrapper));
    
    public ISchemaColumn GetColumnByName(string name)
    {
        return Columns.FirstOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}