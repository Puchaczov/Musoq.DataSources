using System;
using Musoq.Schema.Attributes;

namespace Musoq.DataSources.Time;

internal class TimeEntity(DateTimeOffset value)
{
    [EntityProperty]
    public DateTimeOffset DateTime { get; } = value;

    [EntityProperty]
    public int Second => DateTime.Second;

    [EntityProperty]
    public int Minute => DateTime.Minute;

    [EntityProperty]
    public int Hour => DateTime.Hour;

    [EntityProperty]
    public int Day => DateTime.Day;

    [EntityProperty]
    public int Month => DateTime.Month;

    [EntityProperty]
    public int Year => DateTime.Year;

    [EntityProperty]
    public int DayOfWeek => (int)DateTime.DayOfWeek;

    [EntityProperty]
    public int DayOfYear => DateTime.DayOfYear;

    [EntityProperty]
    public TimeSpan TimeOfDay => DateTime.TimeOfDay;
}
