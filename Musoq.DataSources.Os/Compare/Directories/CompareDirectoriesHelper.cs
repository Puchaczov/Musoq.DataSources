﻿using Musoq.Schema.DataSources;
using System;
using System.Collections.Generic;
using System.IO;
using Musoq.DataSources.Os.Files;
using Musoq.Schema;

namespace Musoq.DataSources.Os.Compare.Directories;

internal static class CompareDirectoriesHelper
{
    public static readonly IReadOnlyDictionary<string, int> CompareDirectoriesNameToIndexMap;
    public static readonly IReadOnlyDictionary<int, Func<CompareDirectoriesResult, object>> CompareDirectoriesIndexToMethodAccessMap;
    public static readonly ISchemaColumn[] CompareDirectoriesColumns;

    static CompareDirectoriesHelper()
    {
        CompareDirectoriesNameToIndexMap = new Dictionary<string, int>
        {
            {nameof(CompareDirectoriesResult.SourceFile), 0},
            {nameof(CompareDirectoriesResult.DestinationFile), 1},
            {nameof(CompareDirectoriesResult.State), 2},
            {nameof(CompareDirectoriesResult.SourceRoot), 3},
            {nameof(CompareDirectoriesResult.DestinationRoot), 4},
            {nameof(CompareDirectoriesResult.SourceFileRelative), 5},
            {nameof(CompareDirectoriesResult.DestinationFileRelative), 6}
        };

        CompareDirectoriesIndexToMethodAccessMap = new Dictionary<int, Func<CompareDirectoriesResult, object>>
        {
            {0, info => info.SourceFile},
            {1, info => info.DestinationFile},
            {2, info => info.State.ToString()},
            {3, info => info.SourceRoot},
            {4, info => info.DestinationRoot},
            {5, info => info.SourceFileRelative},
            {6, info => info.DestinationFileRelative}
        };

        CompareDirectoriesColumns =
        [
            new SchemaColumn(nameof(CompareDirectoriesResult.SourceFile), 0, typeof(FileEntity)),
            new SchemaColumn(nameof(CompareDirectoriesResult.DestinationFile), 1, typeof(FileEntity)),
            new SchemaColumn(nameof(CompareDirectoriesResult.State), 2, typeof(string)),
            new SchemaColumn(nameof(CompareDirectoriesResult.SourceRoot), 3, typeof(DirectoryInfo)),
            new SchemaColumn(nameof(CompareDirectoriesResult.DestinationRoot), 4, typeof(DirectoryInfo)),
            new SchemaColumn(nameof(CompareDirectoriesResult.SourceFileRelative), 5, typeof(string)),
            new SchemaColumn(nameof(CompareDirectoriesResult.DestinationFileRelative), 6, typeof(string))
        ];
    }
}