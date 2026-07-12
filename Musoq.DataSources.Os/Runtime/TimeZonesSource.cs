using System;
using System.Collections.Generic;
using System.Linq;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Os.Runtime;

internal sealed class TimeZonesSource(SourceExecutionContext executionContext)
    : RuntimeDiscoverySourceBase<TimeZoneEntity>(executionContext, "timezones")
{
    protected override IEnumerable<TimeZoneEntity> GetRows()
    {
        return TimeZoneInfo.GetSystemTimeZones()
            .OrderBy(static timeZone => timeZone.Id)
            .Select(static timeZone => new TimeZoneEntity(timeZone));
    }
}
