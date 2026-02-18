using System.Threading;
using System.Threading.Tasks;

namespace Musoq.DataSources.Roslyn.Components.NuGet;

internal interface INuGetRetrievalService
{
    Task<string?> GetMetadataFromPathAsync(NuGetResource? commonResources, string propertyName,
        CancellationToken cancellationToken);

    Task<string?> GetMetadataFromNugetOrgAsync(string baseUrl, NuGetResource? commonResources, string propertyName,
        CancellationToken cancellationToken);

    Task<string?> GetMetadataFromCustomApiAsync(string apiEndpoint, NuGetResource? commonResources, string propertyName,
        CancellationToken cancellationToken);

    Task<string[]> GetPackageVersionsAsync(string packageName, CancellationToken cancellationToken);

    Task<string?> DownloadPackageAsync(string packageName, string packageVersion, string packagePath,
        CancellationToken cancellationToken);
}