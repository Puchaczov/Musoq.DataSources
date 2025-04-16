using System.Collections.Generic;

namespace Musoq.DataSources.Roslyn.Components.NuGet;

/// <summary>
/// Resolves the path to the NuGet cache.
/// </summary>
public interface INuGetCachePathResolver
{
    /// <summary>
    /// Resolves all available NuGet cache paths.
    /// </summary>
    /// <returns>An enumerable of paths to the NuGet caches.</returns>
    IEnumerable<string> ResolveAll();
}