namespace Musoq.DataSources.Roslyn.Components
{
    /// <summary>
    /// Resolves the path to the NuGet cache.
    /// </summary>
    public interface INuGetCachePathResolver
    {
        /// <summary>
        /// Resolves the path to the NuGet cache.
        /// </summary>
        /// <returns></returns>
        string Resolve();
    }
}
