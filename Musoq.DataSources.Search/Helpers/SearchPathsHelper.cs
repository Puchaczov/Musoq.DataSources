#nullable enable

using System;
using System.Collections.Generic;
using Musoq.Schema;
using Musoq.Schema.DataSources;

using Musoq.DataSources.Search.Entities;

namespace Musoq.DataSources.Search.Helpers;

internal static class SearchPathsHelper
{
    public static readonly IReadOnlyDictionary<string, int> NameToIndexMap;

    public static readonly ISchemaColumn[] Columns;

    static SearchPathsHelper()
    {
        NameToIndexMap = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { nameof(SearchPath.Path), 0 },
            { nameof(SearchPath.Origin), 1 },
            { nameof(SearchPath.EntryKind), 2 }
        };

        Columns =
        [
            new SchemaColumn(nameof(SearchPath.Path), 0, typeof(string)),
            new SchemaColumn(nameof(SearchPath.Origin), 1, typeof(string)),
            new SchemaColumn(nameof(SearchPath.EntryKind), 2, typeof(string))
        ];
    }
}
