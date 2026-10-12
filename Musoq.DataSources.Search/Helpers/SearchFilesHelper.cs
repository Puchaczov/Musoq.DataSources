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

    public static readonly ISchemaColumn[] Columns;

    static SearchFilesHelper()
    {
        NameToIndexMap = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { nameof(SearchFile.Path), 0 },
            { nameof(SearchFile.Origin), 1 },
            { nameof(SearchFile.PatternId), 2 }
        };

        Columns =
        [
            new SchemaColumn(nameof(SearchFile.Path), 0, typeof(string)),
            new SchemaColumn(nameof(SearchFile.Origin), 1, typeof(string)),
            new SchemaColumn(nameof(SearchFile.PatternId), 2, typeof(string))
        ];
    }
}
