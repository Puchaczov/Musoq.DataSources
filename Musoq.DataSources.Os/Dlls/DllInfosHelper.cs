using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Os.Dlls;

internal static class DllInfosHelper
{
    public static readonly IReadOnlyDictionary<string, int> DllInfosNameToIndexMap;
    public static readonly IReadOnlyDictionary<int, Func<DllInfo, object?>> DllInfosIndexToMethodAccessMap;
    public static readonly ISchemaColumn[] DllInfosColumns;

    static DllInfosHelper()
    {
        DllInfosNameToIndexMap = new Dictionary<string, int>
        {
            { nameof(DllInfo.FileInfo), 0 },
            { nameof(DllInfo.Assembly), 1 },
            { nameof(DllInfo.Version), 2 }
        };

        DllInfosIndexToMethodAccessMap = new Dictionary<int, Func<DllInfo, object?>>
        {
            { 0, info => info.FileInfo },
            { 1, info => info.Assembly },
            { 2, info => info.Version }
        };

        DllInfosColumns =
        [
            new SchemaColumn(nameof(DllInfo.FileInfo), 0, typeof(FileInfo)),
            new SchemaColumn(nameof(DllInfo.Assembly), 1, typeof(Assembly)),
            new SchemaColumn(nameof(DllInfo.Version), 2, typeof(FileVersionInfo))
        ];
    }
}