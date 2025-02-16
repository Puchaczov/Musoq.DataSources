using System.Threading;
using System.Threading.Tasks;

namespace Musoq.DataSources.Roslyn.Components.NuGet;

internal interface INuGetRetrievalService
{
    Task<string?> GetMetadataFromPathAsync(string packagePath, string packageName, string propertyName, CancellationToken cancellationToken);
        
    Task<string?> GetMetadataFromWebAsync(string baseUrl, string packageName, string packageVersion, CommonResources commonResources, string propertyName, CancellationToken cancellationToken);
        
    Task<string?> GetMetadataFromCustomApiAsync(string apiEndpoint, string packageName, string packageVersion, string propertyName, CancellationToken cancellationToken);
}