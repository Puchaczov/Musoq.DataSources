using System;
using System.Collections.Generic;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Time;

internal static class TimeHelper
{
    public static readonly IReadOnlyDictionary<string, int> TimeNameToIndexMap;
    public static readonly ISchemaColumn[] TimeColumns;

    static TimeHelper()
    {
        TimeNameToIndexMap = new Dictionary<string, int>
        {
            { nameof(TimeEntity.DateTime), 0 },
            { nameof(TimeEntity.Second), 1 },
            { nameof(TimeEntity.Minute), 2 },
            { nameof(TimeEntity.Hour), 3 },
            { nameof(TimeEntity.Day), 4 },
            { nameof(TimeEntity.Month), 5 },
            { nameof(TimeEntity.Year), 6 },
            { nameof(TimeEntity.DayOfWeek), 7 },
            { nameof(TimeEntity.DayOfYear), 8 },
            { nameof(TimeEntity.TimeOfDay), 9 }
        };

        TimeColumns =
        [
            new SchemaColumn(nameof(TimeEntity.DateTime), 0, typeof(DateTimeOffset)),
            new SchemaColumn(nameof(TimeEntity.Second), 1, typeof(int)),
            new SchemaColumn(nameof(TimeEntity.Minute), 2, typeof(int)),
            new SchemaColumn(nameof(TimeEntity.Hour), 3, typeof(int)),
            new SchemaColumn(nameof(TimeEntity.Day), 4, typeof(int)),
            new SchemaColumn(nameof(TimeEntity.Month), 5, typeof(int)),
            new SchemaColumn(nameof(TimeEntity.Year), 6, typeof(int)),
            new SchemaColumn(nameof(TimeEntity.DayOfWeek), 7, typeof(int)),
            new SchemaColumn(nameof(TimeEntity.DayOfYear), 8, typeof(int)),
            new SchemaColumn(nameof(TimeEntity.TimeOfDay), 9, typeof(TimeSpan))
        ];
    }
}
