using System;
using System.Collections.Generic;
using System.IO;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Os.Directories;

internal static class SchemaDirectoriesHelper
{
    public static readonly IReadOnlyDictionary<string, int> DirectoriesNameToIndexMap;
    public static readonly ISchemaColumn[] DirectoriesColumns;

    static SchemaDirectoriesHelper()
    {
        DirectoriesNameToIndexMap = new Dictionary<string, int>
        {
            { nameof(DirectoryInfo.FullName), 0 },
            { nameof(DirectoryInfo.Attributes), 1 },
            { nameof(DirectoryInfo.CreationTime), 2 },
            { nameof(DirectoryInfo.CreationTimeUtc), 3 },
            { nameof(DirectoryInfo.LastAccessTime), 4 },
            { nameof(DirectoryInfo.LastAccessTimeUtc), 5 },
            { nameof(DirectoryInfo.LastWriteTime), 6 },
            { nameof(DirectoryInfo.LastWriteTimeUtc), 7 },
            { nameof(DirectoryInfo.Exists), 8 },
            { nameof(DirectoryInfo.Extension), 9 },
            { nameof(DirectoryInfo.Name), 10 },
            { nameof(DirectoryInfo.Parent), 11 },
            { nameof(DirectoryInfo.Root), 12 },
            { nameof(DirectoryInfo), 13 }
        };

        DirectoriesColumns =
        [
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
        ];
    }
}