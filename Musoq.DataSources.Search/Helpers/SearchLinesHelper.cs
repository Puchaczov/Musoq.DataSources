#nullable enable

using System;
using System.Collections.Generic;
using Musoq.Schema;
using Musoq.Schema.DataSources;

using Musoq.DataSources.Search.Entities;

namespace Musoq.DataSources.Search.Helpers;

internal static class SearchLinesHelper
{
    public static readonly IReadOnlyDictionary<string, int> NameToIndexMap;

    public static readonly ISchemaColumn[] Columns;

    static SearchLinesHelper()
    {
        NameToIndexMap = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { nameof(SearchLine.Path), 0 },
            { nameof(SearchLine.Origin), 1 },
            { nameof(SearchLine.PatternId), 2 },
            { nameof(SearchLine.LineNumber), 3 },
            { nameof(SearchLine.ByteOffset), 4 },
            { nameof(SearchLine.LineText), 5 },
            { nameof(SearchLine.OccurrenceCount), 6 }
        };

        Columns =
        [
            new SchemaColumn(nameof(SearchLine.Path), 0, typeof(string)),
            new SchemaColumn(nameof(SearchLine.Origin), 1, typeof(string)),
            new SchemaColumn(nameof(SearchLine.PatternId), 2, typeof(string)),
            new SchemaColumn(nameof(SearchLine.LineNumber), 3, typeof(long)),
            new SchemaColumn(nameof(SearchLine.ByteOffset), 4, typeof(long?)),
            new SchemaColumn(nameof(SearchLine.LineText), 5, typeof(string)),
            new SchemaColumn(nameof(SearchLine.OccurrenceCount), 6, typeof(long))
        ];
    }
}
