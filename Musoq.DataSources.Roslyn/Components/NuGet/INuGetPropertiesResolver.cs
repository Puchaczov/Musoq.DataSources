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
    /// <param name="licensesContent">The license content</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The licenses names</returns>
    Task<string[]> GetLicensesNamesAsync(string licensesContent, CancellationToken cancellationToken);
}