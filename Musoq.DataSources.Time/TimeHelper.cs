using System;
using System.Collections.Generic;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Time;

internal static class TimeHelper
{
    public static readonly IReadOnlyDictionary<string, int> TimeNameToIndexMap;
    public static readonly IReadOnlyDictionary<int, Func<TimeEntity, object>> TimeIndexToMethodAccessMap;
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

        TimeIndexToMethodAccessMap = new Dictionary<int, Func<TimeEntity, object>>
        {
            { 0, entity => entity.DateTime },
            { 1, entity => entity.Second },
            { 2, entity => entity.Minute },
            { 3, entity => entity.Hour },
            { 4, entity => entity.Day },
            { 5, entity => entity.Month },
            { 6, entity => entity.Year },
            { 7, entity => entity.DayOfWeek },
            { 8, entity => entity.DayOfYear },
            { 9, entity => entity.TimeOfDay }
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
