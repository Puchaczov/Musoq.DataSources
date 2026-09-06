#nullable enable

using System;
using System.Collections.Generic;
using Musoq.Schema;
using Musoq.Schema.DataSources;

using Musoq.DataSources.Search.Entities;

namespace Musoq.DataSources.Search.Helpers;

internal static class SearchMatchesHelper
{
    public static readonly IReadOnlyDictionary<string, int> NameToIndexMap;
    public static readonly IReadOnlyDictionary<int, Func<SearchMatch, object?>> IndexToMethodAccessMap;
    public static readonly ISchemaColumn[] Columns;

    static SearchMatchesHelper()
    {
        NameToIndexMap = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { nameof(SearchMatch.Path), 0 },
            { nameof(SearchMatch.PatternId), 1 },
            { nameof(SearchMatch.MatchIndex), 2 },
            { nameof(SearchMatch.ByteOffset), 3 },
            { nameof(SearchMatch.ByteLength), 4 },
            { nameof(SearchMatch.LineNumber), 5 },
            { nameof(SearchMatch.Utf16Column), 6 },
            { nameof(SearchMatch.Utf16Length), 7 },
            { nameof(SearchMatch.MatchText), 8 },
            { nameof(SearchMatch.Captures), 9 },
            { nameof(SearchMatch.Context), 10 }
        };

        IndexToMethodAccessMap = new Dictionary<int, Func<SearchMatch, object?>>
        {
            { 0, match => match.Path },
            { 1, match => match.PatternId },
            { 2, match => match.MatchIndex },
            { 3, match => match.ByteOffset },
            { 4, match => match.ByteLength },
            { 5, match => match.LineNumber },
            { 6, match => match.Utf16Column },
            { 7, match => match.Utf16Length },
            { 8, match => match.MatchText },
            { 9, match => match.Captures },
            { 10, match => match.Context }
        };

        Columns =
        [
            new SchemaColumn(nameof(SearchMatch.Path), 0, typeof(string)),
            new SchemaColumn(nameof(SearchMatch.PatternId), 1, typeof(string)),
            new SchemaColumn(nameof(SearchMatch.MatchIndex), 2, typeof(long)),
            new SchemaColumn(nameof(SearchMatch.ByteOffset), 3, typeof(long?)),
            new SchemaColumn(nameof(SearchMatch.ByteLength), 4, typeof(long?)),
            new SchemaColumn(nameof(SearchMatch.LineNumber), 5, typeof(long)),
            new SchemaColumn(nameof(SearchMatch.Utf16Column), 6, typeof(long)),
            new SchemaColumn(nameof(SearchMatch.Utf16Length), 7, typeof(long)),
            new SchemaColumn(nameof(SearchMatch.MatchText), 8, typeof(string)),
            new SchemaColumn(nameof(SearchMatch.Captures), 9, typeof(IReadOnlyList<SearchCapture>)),
            new SchemaColumn(nameof(SearchMatch.Context), 10, typeof(IReadOnlyList<SearchContextLine>))
        ];
    }
}
