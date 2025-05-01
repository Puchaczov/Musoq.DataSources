using System.Collections.Generic;
using System.Linq;

namespace Musoq.DataSources.Roslyn.Components.NuGet.Version;

// Represents a wildcard version (e.g., "1.0.*")
internal class WildcardVersionRange(string versionPrefix) : VersionRange
{
    public override IEnumerable<string> ResolveVersions(IEnumerable<string> availableVersions)
    {
        var prefixToMatch = versionPrefix.TrimEnd('*', '.');
        
        // Return all versions that start with the specified prefix
        return availableVersions.Where(v => v.StartsWith(prefixToMatch));
    }
}
