using System.Collections.Generic;
using System.Globalization;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Os.Runtime;

internal sealed class CurrentCultureSource(SourceExecutionContext executionContext)
    : RuntimeDiscoverySourceBase<CurrentCultureEntity>(executionContext, "currentculture")
{
    protected override IEnumerable<CurrentCultureEntity> GetRows()
    {
        yield return new CurrentCultureEntity(CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
    }
}
