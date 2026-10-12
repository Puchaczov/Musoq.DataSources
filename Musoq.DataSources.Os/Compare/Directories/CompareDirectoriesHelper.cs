using System;
using System.Collections.Generic;
using System.IO;
using Musoq.DataSources.Os.Files;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Os.Compare.Directories;

internal static class CompareDirectoriesHelper
{
    public static readonly IReadOnlyDictionary<string, int> CompareDirectoriesNameToIndexMap;

    public static readonly ISchemaColumn[] CompareDirectoriesColumns;

    static CompareDirectoriesHelper()
    {
        CompareDirectoriesNameToIndexMap = new Dictionary<string, int>
        {
            { nameof(CompareDirectoriesResult.SourceFile), 0 },
            { nameof(CompareDirectoriesResult.DestinationFile), 1 },
            { nameof(CompareDirectoriesResult.State), 2 },
            { nameof(CompareDirectoriesResult.SourceRoot), 3 },
            { nameof(CompareDirectoriesResult.DestinationRoot), 4 },
            { nameof(CompareDirectoriesResult.SourceFileRelative), 5 },
            { nameof(CompareDirectoriesResult.DestinationFileRelative), 6 }
        };

        CompareDirectoriesColumns =
        [
            new SchemaColumn(nameof(CompareDirectoriesResult.SourceFile), 0, typeof(FileEntity)),
            new SchemaColumn(nameof(CompareDirectoriesResult.DestinationFile), 1, typeof(FileEntity)),
            new SchemaColumn(nameof(CompareDirectoriesResult.State), 2, typeof(State)),
            new SchemaColumn(nameof(CompareDirectoriesResult.SourceRoot), 3, typeof(DirectoryInfo)),
            new SchemaColumn(nameof(CompareDirectoriesResult.DestinationRoot), 4, typeof(DirectoryInfo)),
            new SchemaColumn(nameof(CompareDirectoriesResult.SourceFileRelative), 5, typeof(string)),
            new SchemaColumn(nameof(CompareDirectoriesResult.DestinationFileRelative), 6, typeof(string))
        ];
    }
}
