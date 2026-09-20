#nullable enable

using System;
using System.Collections.Generic;
using Musoq.Schema;
using Musoq.Schema.DataSources;

using Musoq.DataSources.Search.Entities;

namespace Musoq.DataSources.Search.Helpers;

internal static class SearchFilesHelper
{
    public static readonly IReadOnlyDictionary<string, int> NameToIndexMap;
    public static readonly IReadOnlyDictionary<int, Func<SearchFile, object?>> IndexToMethodAccessMap;
    public static readonly ISchemaColumn[] Columns;

    static SearchFilesHelper()
    {
        NameToIndexMap = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { nameof(SearchFile.Path), 0 },
            { nameof(SearchFile.Origin), 1 },
            { nameof(SearchFile.PatternId), 2 }
        };

        IndexToMethodAccessMap = new Dictionary<int, Func<SearchFile, object?>>
        {
            { 0, file => file.Path },
            { 1, file => file.Origin },
            { 2, file => file.PatternId }
        };

        Columns =
        [
            new SchemaColumn(nameof(SearchFile.Path), 0, typeof(string)),
            new SchemaColumn(nameof(SearchFile.Origin), 1, typeof(string)),
            new SchemaColumn(nameof(SearchFile.PatternId), 2, typeof(string))
        ];
    }
}
