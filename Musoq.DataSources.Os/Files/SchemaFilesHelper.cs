using System;
using System.Collections.Generic;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Os.Files;

internal static class SchemaFilesHelper
{
    public static readonly IReadOnlyDictionary<string, int> FilesNameToIndexMap;
    public static readonly IReadOnlyDictionary<int, Func<FileEntity, object?>> FilesIndexToMethodAccessMap;
    public static readonly ISchemaColumn[] FilesColumns;

    static SchemaFilesHelper()
    {
        FilesNameToIndexMap = new Dictionary<string, int>
        {
            {nameof(FileEntity.Name), 0},
            {nameof(FileEntity.CreationTime), 1},
            {nameof(FileEntity.CreationTimeUtc), 2},
            {nameof(FileEntity.LastAccessTime), 3},
            {nameof(FileEntity.LastAccessTimeUtc), 4},
            {nameof(FileEntity.LastWriteTime), 5},
            {nameof(FileEntity.LastWriteTimeUtc), 6},
            {nameof(FileEntity.DirectoryName), 7},
            {nameof(FileEntity.Extension), 8},
            {nameof(FileEntity.FullPath), 9},
            {nameof(FileEntity.Exists), 10},
            {nameof(FileEntity.IsReadOnly), 11},
            {nameof(FileEntity.Length), 12},
        };

        FilesIndexToMethodAccessMap = new Dictionary<int, Func<FileEntity, object?>>
        {
            {0, info => info.Name},
            {1, info => info.CreationTime},
            {2, info => info.CreationTimeUtc},
            {3, info => info.LastAccessTime},
            {4, info => info.LastAccessTimeUtc},
            {5, info => info.LastWriteTime},
            {6, info => info.LastWriteTimeUtc},
            {7, info => info.DirectoryName},
            {8, info => info.Extension},
            {9, info => info.FullPath},
            {10, info => info.Exists},
            {11, info => info.IsReadOnly},
            {12, info => info.Length}
        };

        FilesColumns =
        [
            new SchemaColumn(nameof(FileEntity.Name), 0, typeof(string)),
            new SchemaColumn(nameof(FileEntity.CreationTime), 1, typeof(DateTimeOffset)),
            new SchemaColumn(nameof(FileEntity.CreationTimeUtc), 2, typeof(DateTimeOffset)),
            new SchemaColumn(nameof(FileEntity.LastAccessTime), 3, typeof(DateTimeOffset)),
            new SchemaColumn(nameof(FileEntity.LastAccessTimeUtc), 4, typeof(DateTimeOffset)),
            new SchemaColumn(nameof(FileEntity.LastWriteTime), 5, typeof(DateTimeOffset)),
            new SchemaColumn(nameof(FileEntity.LastWriteTimeUtc), 6, typeof(DateTimeOffset)),
            new SchemaColumn(nameof(FileEntity.DirectoryName), 7, typeof(string)),
            new SchemaColumn(nameof(FileEntity.Extension), 8, typeof(string)),
            new SchemaColumn(nameof(FileEntity.FullPath), 9, typeof(string)),
            new SchemaColumn(nameof(FileEntity.Exists), 10, typeof(bool)),
            new SchemaColumn(nameof(FileEntity.IsReadOnly), 11, typeof(bool)),
            new SchemaColumn(nameof(FileEntity.Length), 12, typeof(long))
        ];
    }
}