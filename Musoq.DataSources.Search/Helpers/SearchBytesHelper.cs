#nullable enable

using System;
using System.Collections.Generic;
using Musoq.Schema;
using Musoq.Schema.DataSources;

using Musoq.DataSources.Search.Entities;

namespace Musoq.DataSources.Search.Helpers;

internal static class SearchBytesHelper
{
    public static readonly IReadOnlyDictionary<string, int> NameToIndexMap;
    public static readonly IReadOnlyDictionary<int, Func<SearchByteMatch, object?>> IndexToMethodAccessMap;
    public static readonly ISchemaColumn[] Columns;

    static SearchBytesHelper()
    {
        NameToIndexMap = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { nameof(SearchByteMatch.Path), 0 },
            { nameof(SearchByteMatch.Origin), 1 },
            { nameof(SearchByteMatch.PatternId), 2 },
            { nameof(SearchByteMatch.MatchIndex), 3 },
            { nameof(SearchByteMatch.ByteOffset), 4 },
            { nameof(SearchByteMatch.ByteLength), 5 },
            { nameof(SearchByteMatch.MatchedBytes), 6 },
            { nameof(SearchByteMatch.WindowStartByteOffset), 7 },
            { nameof(SearchByteMatch.WindowByteLength), 8 },
            { nameof(SearchByteMatch.WindowComplete), 9 },
            { nameof(SearchByteMatch.WindowBytes), 10 },
            { nameof(SearchByteMatch.LineNumber), 11 },
            { nameof(SearchByteMatch.Utf16Column), 12 },
            { nameof(SearchByteMatch.Utf16Length), 13 },
            { nameof(SearchByteMatch.MatchText), 14 },
            { nameof(SearchByteMatch.LineText), 15 },
            { nameof(SearchByteMatch.Captures), 16 },
            { nameof(SearchByteMatch.Context), 17 }
        };

        IndexToMethodAccessMap = new Dictionary<int, Func<SearchByteMatch, object?>>
        {
            { 0, match => match.Path },
            { 1, match => match.Origin },
            { 2, match => match.PatternId },
            { 3, match => match.MatchIndex },
            { 4, match => match.ByteOffset },
            { 5, match => match.ByteLength },
            { 6, match => match.MatchedBytes },
            { 7, match => match.WindowStartByteOffset },
            { 8, match => match.WindowByteLength },
            { 9, match => match.WindowComplete },
            { 10, match => match.WindowBytes },
            { 11, match => match.LineNumber },
            { 12, match => match.Utf16Column },
            { 13, match => match.Utf16Length },
            { 14, match => match.MatchText },
            { 15, match => match.LineText },
            { 16, match => match.Captures },
            { 17, match => match.Context }
        };

        Columns =
        [
            new SchemaColumn(nameof(SearchByteMatch.Path), 0, typeof(string)),
            new SchemaColumn(nameof(SearchByteMatch.Origin), 1, typeof(string)),
            new SchemaColumn(nameof(SearchByteMatch.PatternId), 2, typeof(string)),
            new SchemaColumn(nameof(SearchByteMatch.MatchIndex), 3, typeof(long)),
            new SchemaColumn(nameof(SearchByteMatch.ByteOffset), 4, typeof(long)),
            new SchemaColumn(nameof(SearchByteMatch.ByteLength), 5, typeof(long)),
            new SchemaColumn(nameof(SearchByteMatch.MatchedBytes), 6, typeof(byte[])),
            new SchemaColumn(nameof(SearchByteMatch.WindowStartByteOffset), 7, typeof(long?)),
            new SchemaColumn(nameof(SearchByteMatch.WindowByteLength), 8, typeof(long?)),
            new SchemaColumn(nameof(SearchByteMatch.WindowComplete), 9, typeof(bool?)),
            new SchemaColumn(nameof(SearchByteMatch.WindowBytes), 10, typeof(byte[])),
            new SchemaColumn(nameof(SearchByteMatch.LineNumber), 11, typeof(long?)),
            new SchemaColumn(nameof(SearchByteMatch.Utf16Column), 12, typeof(long?)),
            new SchemaColumn(nameof(SearchByteMatch.Utf16Length), 13, typeof(long?)),
            new SchemaColumn(nameof(SearchByteMatch.MatchText), 14, typeof(string)),
            new SchemaColumn(nameof(SearchByteMatch.LineText), 15, typeof(string)),
            new SchemaColumn(nameof(SearchByteMatch.Captures), 16, typeof(IReadOnlyList<SearchCapture>)),
            new SchemaColumn(nameof(SearchByteMatch.Context), 17, typeof(IReadOnlyList<SearchContextLine>))
        ];
    }
}
