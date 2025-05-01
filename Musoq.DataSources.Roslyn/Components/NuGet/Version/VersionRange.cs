using System.Collections.Generic;

namespace Musoq.DataSources.Roslyn.Components.NuGet.Version;

// Base class for version ranges
internal abstract class VersionRange
{
    public abstract IEnumerable<string> ResolveVersions(IEnumerable<string> availableVersions);
}