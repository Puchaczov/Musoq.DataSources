using System;
using System.Collections.Generic;
using System.IO;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Os.Directories;

internal static class SchemaDirectoriesHelper
{
    public static readonly IDictionary<string, int> DirectoriesNameToIndexMap;
    public static readonly IDictionary<int, Func<DirectoryInfo, object?>> DirectoriesIndexToMethodAccessMap;
    public static readonly ISchemaColumn[] DirectoriesColumns;

    static SchemaDirectoriesHelper()
    {
        DirectoriesNameToIndexMap = new Dictionary<string, int>
        {
            {nameof(DirectoryInfo.FullName), 0},
            {nameof(DirectoryInfo.Attributes), 1},
            {nameof(DirectoryInfo.CreationTime), 2},
            {nameof(DirectoryInfo.CreationTimeUtc), 3},
            {nameof(DirectoryInfo.LastAccessTime), 4},
            {nameof(DirectoryInfo.LastAccessTimeUtc), 5},
            {nameof(DirectoryInfo.LastWriteTime), 6},
            {nameof(DirectoryInfo.LastWriteTimeUtc), 7},
            {nameof(DirectoryInfo.Exists), 8},
            {nameof(DirectoryInfo.Extension), 9},
            {nameof(DirectoryInfo.Name), 10},
            {nameof(DirectoryInfo.Parent), 11},
            {nameof(DirectoryInfo.Root), 12},
            {nameof(DirectoryInfo), 13}
        };

        DirectoriesIndexToMethodAccessMap = new Dictionary<int, Func<DirectoryInfo, object?>>
        {
            {0, info => info.FullName},
            {1, info => info.Attributes},
            {2, info => info.CreationTime},
            {3, info => info.CreationTimeUtc},
            {4, info => info.LastAccessTime},
            {5, info => info.LastAccessTimeUtc},
            {6, info => info.LastWriteTime},
            {7, info => info.LastWriteTimeUtc},
            {8, info => info.Exists},
            {9, info => info.Extension},
            {10, info => info.Name},
            {11, info => info.Parent},
            {12, info => info.Root},
            {13, info => info}
        };

        DirectoriesColumns = new ISchemaColumn[]
        {
            new SchemaColumn(nameof(DirectoryInfo.FullName), 0, typeof(string)),
            new SchemaColumn(nameof(DirectoryInfo.Attributes), 1, typeof(FileAttributes)),
            new SchemaColumn(nameof(DirectoryInfo.CreationTime), 2, typeof(DateTimeOffset)),
            new SchemaColumn(nameof(DirectoryInfo.CreationTimeUtc), 3, typeof(DateTimeOffset)),
            new SchemaColumn(nameof(DirectoryInfo.LastAccessTime), 4, typeof(DateTimeOffset)),
            new SchemaColumn(nameof(DirectoryInfo.LastAccessTimeUtc), 5, typeof(DateTimeOffset)),
            new SchemaColumn(nameof(DirectoryInfo.LastWriteTime), 6, typeof(DateTimeOffset)),
            new SchemaColumn(nameof(DirectoryInfo.LastWriteTimeUtc), 7, typeof(DateTimeOffset)),
            new SchemaColumn(nameof(DirectoryInfo.Exists), 8, typeof(bool)),
            new SchemaColumn(nameof(DirectoryInfo.Extension), 9, typeof(string)),
            new SchemaColumn(nameof(DirectoryInfo.Name), 10, typeof(string)),
            new SchemaColumn(nameof(DirectoryInfo.Parent), 11, typeof(DirectoryInfo)),
            new SchemaColumn(nameof(DirectoryInfo.Root), 12, typeof(string)),
            new SchemaColumn(nameof(DirectoryInfo), 13, typeof(DirectoryInfo))
        };
    }
}