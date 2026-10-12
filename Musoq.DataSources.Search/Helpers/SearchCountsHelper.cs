#nullable enable

using System;
using System.Collections.Generic;
using Musoq.Schema;
using Musoq.Schema.DataSources;

using Musoq.DataSources.Search.Entities;

namespace Musoq.DataSources.Search.Helpers;

internal static class SearchCountsHelper
{
    public static readonly IReadOnlyDictionary<string, int> NameToIndexMap;

    public static readonly ISchemaColumn[] Columns;

    static SearchCountsHelper()
    {
        NameToIndexMap = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { nameof(SearchCount.Path), 0 },
            { nameof(SearchCount.Origin), 1 },
            { nameof(SearchCount.PatternId), 2 },
            { nameof(SearchCount.OccurrenceCount), 3 },
            { nameof(SearchCount.MatchingLineCount), 4 },
            { nameof(SearchCount.BytesScanned), 5 },
            { nameof(SearchCount.Complete), 6 }
        };

        Columns =
        [
            new SchemaColumn(nameof(SearchCount.Path), 0, typeof(string)),
            new SchemaColumn(nameof(SearchCount.Origin), 1, typeof(string)),
            new SchemaColumn(nameof(SearchCount.PatternId), 2, typeof(string)),
            new SchemaColumn(nameof(SearchCount.OccurrenceCount), 3, typeof(long)),
            new SchemaColumn(nameof(SearchCount.MatchingLineCount), 4, typeof(long)),
            new SchemaColumn(nameof(SearchCount.BytesScanned), 5, typeof(long)),
            new SchemaColumn(nameof(SearchCount.Complete), 6, typeof(bool))
        ];
    }
}
