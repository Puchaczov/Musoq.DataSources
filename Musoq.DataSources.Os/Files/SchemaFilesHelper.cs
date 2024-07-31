using System;
using System.Collections.Generic;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Os.Files;

internal static class SchemaFilesHelper
{
    public static readonly IDictionary<string, int> FilesNameToIndexMap;
    public static readonly IDictionary<int, Func<ExtendedFileInfo, object?>> FilesIndexToMethodAccessMap;
    public static readonly ISchemaColumn[] FilesColumns;

    static SchemaFilesHelper()
    {
        FilesNameToIndexMap = new Dictionary<string, int>
        {
            {nameof(ExtendedFileInfo.Name), 0},
            {nameof(ExtendedFileInfo.CreationTime), 1},
            {nameof(ExtendedFileInfo.CreationTimeUtc), 2},
            {nameof(ExtendedFileInfo.LastAccessTime), 3},
            {nameof(ExtendedFileInfo.LastAccessTimeUtc), 4},
            {nameof(ExtendedFileInfo.LastWriteTime), 5},
            {nameof(ExtendedFileInfo.LastWriteTimeUtc), 6},
            {nameof(ExtendedFileInfo.DirectoryName), 7},
            {nameof(ExtendedFileInfo.Extension), 8},
            {nameof(ExtendedFileInfo.FullName), 9},
            {nameof(ExtendedFileInfo.Exists), 10},
            {nameof(ExtendedFileInfo.IsReadOnly), 11},
            {nameof(ExtendedFileInfo.Length), 12},
        };

        FilesIndexToMethodAccessMap = new Dictionary<int, Func<ExtendedFileInfo, object?>>
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
            {9, info => info.FullName},
            {10, info => info.Exists},
            {11, info => info.IsReadOnly},
            {12, info => info.Length}
        };

        FilesColumns =
        [
            new SchemaColumn(nameof(ExtendedFileInfo.Name), 0, typeof(string)),
            new SchemaColumn(nameof(ExtendedFileInfo.CreationTime), 1, typeof(DateTimeOffset)),
            new SchemaColumn(nameof(ExtendedFileInfo.CreationTimeUtc), 2, typeof(DateTimeOffset)),
            new SchemaColumn(nameof(ExtendedFileInfo.LastAccessTime), 3, typeof(DateTimeOffset)),
            new SchemaColumn(nameof(ExtendedFileInfo.LastAccessTimeUtc), 4, typeof(DateTimeOffset)),
            new SchemaColumn(nameof(ExtendedFileInfo.LastWriteTime), 5, typeof(DateTimeOffset)),
            new SchemaColumn(nameof(ExtendedFileInfo.LastWriteTimeUtc), 6, typeof(DateTimeOffset)),
            new SchemaColumn(nameof(ExtendedFileInfo.DirectoryName), 7, typeof(string)),
            new SchemaColumn(nameof(ExtendedFileInfo.Extension), 8, typeof(string)),
            new SchemaColumn(nameof(ExtendedFileInfo.FullName), 9, typeof(string)),
            new SchemaColumn(nameof(ExtendedFileInfo.Exists), 10, typeof(bool)),
            new SchemaColumn(nameof(ExtendedFileInfo.IsReadOnly), 11, typeof(bool)),
            new SchemaColumn(nameof(ExtendedFileInfo.Length), 12, typeof(long))
        ];
    }
}