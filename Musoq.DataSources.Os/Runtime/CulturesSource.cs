using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Os.Runtime;

internal sealed class CulturesSource(SourceExecutionContext executionContext)
    : RuntimeDiscoverySourceBase<CultureEntity>(executionContext, "cultures")
{
    protected override IEnumerable<CultureEntity> GetRows()
    {
        return CultureInfo.GetCultures(CultureTypes.AllCultures)
            .OrderBy(static culture => culture.Name)
            .Select(static culture => new CultureEntity(culture));
    }
}
