using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Musoq.DataSources.Roslyn.Components.NuGet;

/// <summary>
/// Resolves properties based on the AI model
/// </summary>
public interface IAiBasedPropertiesResolver
{
    /// <summary>
    /// Gets the license names from the license content
    /// </summary>
    /// <param name="licenseContent">The license content</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The license names</returns>
    Task<string[]> GetLicenseNamesAsync(string licenseContent, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the license names from the content of license URL
    /// </summary>
    /// <param name="licenseUrl">The license URL</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The license names</returns>
    Task<string[]> GetLicenseNamesByLicenseUrlAsync(string licenseUrl, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the license names from the content of the source repository URL
    /// </summary>
    /// <param name="sourceRepositoryUrl">The source repository URL</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The license names</returns>
    Task<string[]> GetLicenseNamesBySourceRepositoryUrlAsync(string sourceRepositoryUrl, CancellationToken cancellationToken);
}