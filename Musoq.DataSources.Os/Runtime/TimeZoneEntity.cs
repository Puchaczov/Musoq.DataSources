using System;

namespace Musoq.DataSources.Os.Runtime;

internal sealed class TimeZoneEntity(TimeZoneInfo timeZone)
{
    public string Id => timeZone.Id;
    public string DisplayName => timeZone.DisplayName;
    public string StandardName => timeZone.StandardName;
    public string DaylightName => timeZone.DaylightName;
    public TimeSpan BaseUtcOffset => timeZone.BaseUtcOffset;
    public bool SupportsDaylightSavingTime => timeZone.SupportsDaylightSavingTime;
}
