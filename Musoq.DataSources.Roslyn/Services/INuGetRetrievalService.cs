using System.Threading;
using System.Threading.Tasks;
using Musoq.DataSources.Roslyn.Components;
using Musoq.DataSources.Roslyn.Components.NuGet;

namespace Musoq.DataSources.Roslyn.Services
{
    internal interface INuGetRetrievalService
    {
        IFileSystem FileSystem { get; } // Add this line

        string ResolvePackagePath(INuGetCachePathResolver nuGetCachePathResolver, string packageName, string packageVersion);
        Task<string?> GetMetadataFromPathAsync(string packagePath, string packageName, string propertyName, CancellationToken cancellationToken);
        Task<string?> GetMetadataFromWebAsync(string baseUrl, string packageName, string packageVersion, CommonResources commonResources, string propertyName, CancellationToken cancellationToken);
        Task<string?> GetMetadataFromCustomApiAsync(string apiEndpoint, string packageName, string packageVersion, string propertyName, CancellationToken cancellationToken);
    }
}
