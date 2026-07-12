using System.Collections.Generic;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Os.Runtime;

internal sealed class PathInfoSource(string path, SourceExecutionContext executionContext)
    : RuntimeDiscoverySourceBase<PathInfoEntity>(executionContext, "pathinfo")
{
    protected override IEnumerable<PathInfoEntity> GetRows()
    {
        yield return new PathInfoEntity(path);
    }
}
