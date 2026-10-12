using System;
using System.Collections.Generic;

namespace Musoq.DataSources.System;

internal static class SystemSchemaHelper
{
    public static readonly IReadOnlyDictionary<string, int> FlatNameToIndexMap;

    static SystemSchemaHelper()
    {
        FlatNameToIndexMap = new Dictionary<string, int>
        {
            { nameof(DualEntity.Dummy), 0 }
        };

    }
}