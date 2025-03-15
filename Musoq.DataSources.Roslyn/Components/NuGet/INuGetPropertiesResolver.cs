using System.Threading;
using System.Threading.Tasks;

namespace Musoq.DataSources.Roslyn.Components.NuGet;

/// <summary>
/// Resolves properties based on the AI model
/// </summary>
public interface INuGetPropertiesResolver
{
    /// <summary>
    /// Gets the license names from the license content
    /// </summary>
    /// <param name="licenseContent">The license content</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The license names</returns>
    Task<string[]> GetLicenseNamesAsync(string licenseContent, CancellationToken cancellationToken);
}