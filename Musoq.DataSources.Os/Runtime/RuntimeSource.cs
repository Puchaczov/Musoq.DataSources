using System.Collections.Generic;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Os.Runtime;

internal sealed class RuntimeSource(SourceExecutionContext executionContext)
    : RuntimeDiscoverySourceBase<RuntimeEntity>(executionContext, "runtime")
{
    protected override IEnumerable<RuntimeEntity> GetRows()
    {
        yield return new RuntimeEntity();
    }
}
