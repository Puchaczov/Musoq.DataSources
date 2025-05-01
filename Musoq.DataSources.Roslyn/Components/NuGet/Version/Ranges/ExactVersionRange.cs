using System.Collections.Generic;
using System.Linq;

namespace Musoq.DataSources.Roslyn.Components.NuGet.Version.Ranges;

// Represents an exact version (e.g., "4.8.0")
internal class ExactVersionRange(string version) : VersionRange
{
    public override IEnumerable<string> ResolveVersions(IEnumerable<string> availableVersions)
    {
        // Return just the exact version if it's available
        return availableVersions.Where(v => v == version);
    }
}
