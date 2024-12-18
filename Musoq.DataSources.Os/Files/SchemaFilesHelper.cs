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
            {nameof(FileEntity.FileName), 1},
            {nameof(FileEntity.CreationTime), 2},
            {nameof(FileEntity.CreationTimeUtc), 3},
            {nameof(FileEntity.LastAccessTime), 4},
            {nameof(FileEntity.LastAccessTimeUtc), 5},
            {nameof(FileEntity.LastWriteTime), 6},
            {nameof(FileEntity.LastWriteTimeUtc), 7},
            {nameof(FileEntity.DirectoryName), 8},
            {nameof(FileEntity.DirectoryPath), 9},
            {nameof(FileEntity.Extension), 10},
            {nameof(FileEntity.FullPath), 11},
            {nameof(FileEntity.Exists), 12},
            {nameof(FileEntity.IsReadOnly), 13},
            {nameof(FileEntity.Length), 14},
        };

        FilesIndexToMethodAccessMap = new Dictionary<int, Func<FileEntity, object?>>
        {
            {0, entity => entity.Name},
            {1, entity => entity.FileName},
            {2, entity => entity.CreationTime},
            {3, entity => entity.CreationTimeUtc},
            {4, entity => entity.LastAccessTime},
            {5, entity => entity.LastAccessTimeUtc},
            {6, entity => entity.LastWriteTime},
            {7, entity => entity.LastWriteTimeUtc},
            {8, entity => entity.DirectoryName},
            {9, entity => entity.DirectoryPath},
            {10, entity => entity.Extension},
            {11, entity => entity.FullPath},
            {12, entity => entity.Exists},
            {13, entity => entity.IsReadOnly},
            {14, entity => entity.Length}
        };

        FilesColumns =
        [
            new SchemaColumn(nameof(FileEntity.Name), 0, typeof(string)),
            new SchemaColumn(nameof(FileEntity.FileName), 1, typeof(string)),
            new SchemaColumn(nameof(FileEntity.CreationTime), 2, typeof(DateTimeOffset)),
            new SchemaColumn(nameof(FileEntity.CreationTimeUtc), 3, typeof(DateTimeOffset)),
            new SchemaColumn(nameof(FileEntity.LastAccessTime), 4, typeof(DateTimeOffset)),
            new SchemaColumn(nameof(FileEntity.LastAccessTimeUtc), 5, typeof(DateTimeOffset)),
            new SchemaColumn(nameof(FileEntity.LastWriteTime), 6, typeof(DateTimeOffset)),
            new SchemaColumn(nameof(FileEntity.LastWriteTimeUtc), 7, typeof(DateTimeOffset)),
            new SchemaColumn(nameof(FileEntity.DirectoryName), 8, typeof(string)),
            new SchemaColumn(nameof(FileEntity.DirectoryPath), 9, typeof(string)),
            new SchemaColumn(nameof(FileEntity.Extension), 10, typeof(string)),
            new SchemaColumn(nameof(FileEntity.FullPath), 11, typeof(string)),
            new SchemaColumn(nameof(FileEntity.Exists), 12, typeof(bool)),
            new SchemaColumn(nameof(FileEntity.IsReadOnly), 13, typeof(bool)),
            new SchemaColumn(nameof(FileEntity.Length), 14, typeof(long))
        ];
    }
}