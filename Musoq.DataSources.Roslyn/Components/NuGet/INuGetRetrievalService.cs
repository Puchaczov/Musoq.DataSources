using System.Threading;
using System.Threading.Tasks;

namespace Musoq.DataSources.Roslyn.Components.NuGet;

internal interface INuGetRetrievalService
{
    Task<string?> GetMetadataFromPathAsync(CommonResources commonResources, string propertyName, CancellationToken cancellationToken);
        
    Task<string?> GetMetadataFromNugetOrgAsync(string baseUrl, CommonResources commonResources, string propertyName, CancellationToken cancellationToken);
        
    Task<string?> GetMetadataFromCustomApiAsync(string apiEndpoint, CommonResources commonResources, string propertyName, CancellationToken cancellationToken);
    
    Task<string?> DownloadPackageAsync(string packageName, string packageVersion, string packagePath, CancellationToken cancellationToken);
}