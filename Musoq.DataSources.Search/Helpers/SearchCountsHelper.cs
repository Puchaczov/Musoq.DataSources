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
    public static readonly IReadOnlyDictionary<int, Func<SearchCount, object?>> IndexToMethodAccessMap;
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

        IndexToMethodAccessMap = new Dictionary<int, Func<SearchCount, object?>>
        {
            { 0, count => count.Path },
            { 1, count => count.Origin },
            { 2, count => count.PatternId },
            { 3, count => count.OccurrenceCount },
            { 4, count => count.MatchingLineCount },
            { 5, count => count.BytesScanned },
            { 6, count => count.Complete }
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
