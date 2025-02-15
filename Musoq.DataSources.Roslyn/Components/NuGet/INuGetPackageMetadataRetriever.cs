using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Musoq.DataSources.Roslyn.Components.NuGet
{
    /// <summary>
    /// Represents a component that retrieves metadata of NuGet packages.
    /// </summary>
    public interface INuGetPackageMetadataRetriever
    {
        /// <summary>
        /// Gets the metadata of the specified NuGet package.
        /// </summary>
        /// <param name="packageName">The name of the NuGet package.</param>
        /// <param name="packageVersion">The version of the NuGet package.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The metadata of the specified NuGet package.</returns>
        Task<IReadOnlyDictionary<string, string?>> GetMetadataAsync(string packageName, string packageVersion, CancellationToken cancellationToken);
    }
}
