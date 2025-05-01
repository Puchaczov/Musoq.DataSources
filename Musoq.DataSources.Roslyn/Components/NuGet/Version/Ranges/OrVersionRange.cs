using System.Collections.Generic;
using System.Linq;

namespace Musoq.DataSources.Roslyn.Components.NuGet.Version.Ranges;

// Represents OR of two version ranges (e.g., "[1.0.0, 2.0.0) || [3.0.0, 4.0.0)")
internal class OrVersionRange(VersionRange left, VersionRange right) : VersionRange
{
    public override IEnumerable<string> ResolveVersions(IEnumerable<string> availableVersions)
    {
        var leftResolved = left.ResolveVersions(availableVersions);
        var rightResolved = right.ResolveVersions(availableVersions);
        
        return leftResolved.Union(rightResolved);
    }
}