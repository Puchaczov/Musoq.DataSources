using System;
using System.Collections.Generic;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Os.Zip;

internal static class SchemaZipHelper
{
    public static readonly IReadOnlyDictionary<string, int> NameToIndexMap;
    public static readonly IReadOnlyDictionary<int, Func<ZipEntryEntity, object>> IndexToMethodAccessMap;
    public static readonly ISchemaColumn[] SchemaColumns;

    static SchemaZipHelper()
    {
        NameToIndexMap = new Dictionary<string, int>
        {
            { nameof(ZipEntryEntity.Name), 0 },
            { nameof(ZipEntryEntity.FullName), 1 },
            { nameof(ZipEntryEntity.CompressedLength), 2 },
            { nameof(ZipEntryEntity.LastWriteTime), 3 },
            { nameof(ZipEntryEntity.Length), 4 },
            { nameof(ZipEntryEntity.IsDirectory), 5 },
            { nameof(ZipEntryEntity.Level), 6 }
        };

        IndexToMethodAccessMap = new Dictionary<int, Func<ZipEntryEntity, object>>
        {
            { 0, info => info.Name },
            { 1, info => info.FullName },
            { 2, info => info.CompressedLength },
            { 3, info => info.LastWriteTime },
            { 4, info => info.Length },
            { 5, info => info.IsDirectory },
            { 6, info => info.Level }
        };

        SchemaColumns =
        [
            new SchemaColumn(nameof(ZipEntryEntity.Name), 0, typeof(string)),
            new SchemaColumn(nameof(ZipEntryEntity.FullName), 1, typeof(string)),
            new SchemaColumn(nameof(ZipEntryEntity.CompressedLength), 2, typeof(long)),
            new SchemaColumn(nameof(ZipEntryEntity.LastWriteTime), 3, typeof(DateTimeOffset)),
            new SchemaColumn(nameof(ZipEntryEntity.Length), 4, typeof(long)),
            new SchemaColumn(nameof(ZipEntryEntity.IsDirectory), 5, typeof(bool)),
            new SchemaColumn(nameof(ZipEntryEntity.Level), 6, typeof(int))
        ];
    }
}
