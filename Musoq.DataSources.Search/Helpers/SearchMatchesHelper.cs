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
