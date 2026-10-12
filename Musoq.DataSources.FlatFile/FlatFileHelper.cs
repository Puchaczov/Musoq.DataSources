using System;
using System.Collections.Generic;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.FlatFile;

internal static class FlatFileHelper
{
    public static readonly IReadOnlyDictionary<string, int> FlatNameToIndexMap;
    public static readonly ISchemaColumn[] FlatColumns;

    static FlatFileHelper()
    {
        FlatNameToIndexMap = new Dictionary<string, int>
        {
            { nameof(FlatFileEntity.LineNumber), 0 },
            { nameof(FlatFileEntity.Line), 1 }
        };

        FlatColumns =
        [
            new SchemaColumn(nameof(FlatFileEntity.LineNumber), 0, typeof(int)),
            new SchemaColumn(nameof(FlatFileEntity.Line), 1, typeof(string))
        ];
    }
}