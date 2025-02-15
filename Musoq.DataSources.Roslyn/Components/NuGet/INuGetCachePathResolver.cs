namespace Musoq.DataSources.Roslyn.Components.NuGet
{
    /// <summary>
    /// Resolves the path to the NuGet cache.
    /// </summary>
    public interface INuGetCachePathResolver
    {
        /// <summary>
        /// Resolves the path to the NuGet cache.
        /// </summary>
        /// <returns>Path to the NuGet cache.</returns>
        string Resolve();
    }
}
