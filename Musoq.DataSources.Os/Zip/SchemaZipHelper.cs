using System;
using System.Collections.Generic;
using System.IO.Compression;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Os.Zip;

internal static class SchemaZipHelper
{
    public static readonly IReadOnlyDictionary<string, int> NameToIndexMap;
    public static readonly IReadOnlyDictionary<int, Func<ZipArchiveEntry, object>> IndexToMethodAccessMap;
    public static readonly ISchemaColumn[] SchemaColumns;

    static SchemaZipHelper()
    {
        NameToIndexMap = new Dictionary<string, int>
        {
            { nameof(ZipArchiveEntry.Name), 0 },
            { nameof(ZipArchiveEntry.FullName), 1 },
            { nameof(ZipArchiveEntry.CompressedLength), 2 },
            { nameof(ZipArchiveEntry.LastWriteTime), 3 },
            { nameof(ZipArchiveEntry.Length), 4 },
            { "IsDirectory", 5 },
            { "Level", 6 }
        };

        IndexToMethodAccessMap = new Dictionary<int, Func<ZipArchiveEntry, object>>
        {
            { 0, info => info.Name },
            { 1, info => info.FullName },
            { 2, info => info.CompressedLength },
            { 3, info => info.LastWriteTime },
            { 4, info => info.Length },
            { 5, info => info.Name == string.Empty },
            { 6, info => info.FullName.Trim('/').Split('/').Length - 1 }
        };

        SchemaColumns =
        [
            new SchemaColumn(nameof(ZipArchiveEntry.Name), 0, typeof(string)),
            new SchemaColumn(nameof(ZipArchiveEntry.FullName), 1, typeof(string)),
            new SchemaColumn(nameof(ZipArchiveEntry.CompressedLength), 2, typeof(long)),
            new SchemaColumn(nameof(ZipArchiveEntry.LastWriteTime), 3, typeof(DateTimeOffset)),
            new SchemaColumn(nameof(ZipArchiveEntry.Length), 4, typeof(long)),
            new SchemaColumn("IsDirectory", 5, typeof(bool)),
            new SchemaColumn("Level", 6, typeof(int))
        ];
    }
}