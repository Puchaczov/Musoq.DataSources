using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Musoq.DataSources.Roslyn.Components.NuGet;

/// <summary>
/// Represents a NuGet package metadata retriever that retrieves metadata
/// </summary>
/// <param name="nuGetCachePathResolver">Resolves the path to the NuGet cache.</param>
/// <param name="customApiEndpoint">The custom API endpoint to use for last resort metadata retrieval.</param>
internal sealed class NuGetPackageMetadataRetriever(
    INuGetCachePathResolver nuGetCachePathResolver,
    string? customApiEndpoint,
    INuGetRetrievalService retrievalService,
    IFileSystem fileSystem)
    : INuGetPackageMetadataRetriever
{
    /// <summary>
    /// Gets the metadata of the specified NuGet package.
    /// </summary>
    /// <param name="packageName">The name of the NuGet package.</param>
    /// <param name="packageVersion">The version of the NuGet package.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The metadata of the specified NuGet package.</returns>
    public async IAsyncEnumerable<IReadOnlyDictionary<string, string?>> GetMetadataAsync(
        string packageName,
        string packageVersion,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var commonResources = new NuGetResource
        {
            PackagePath = GetPackageCachePath(nuGetCachePathResolver, packageName, packageVersion) ?? 
                          await TryDownloadPackageAsync(packageName, packageVersion, cancellationToken),
            PackageName = packageName,
            PackageVersion = packageVersion
        };
        
        var retrieveCommonResourcesVisitor = new NuGetResourceVisitor(
            commonResources,
            retrievalService,
            customApiEndpoint);
        
        await commonResources.AcceptAsync(retrieveCommonResourcesVisitor, cancellationToken);
        
        var licenses = commonResources.Licenses;

        if (licenses.Length == 0)
        {
            yield return BuildMetadata(null, commonResources);
        }
        else
        {
            yield return BuildMetadata(licenses[0], commonResources);
            
            for (var i = 1; i < licenses.Length; i++)
            {
                yield return BuildMetadata(licenses[i], commonResources);
            }
        }
    }

    private string? GetPackageCachePath(INuGetCachePathResolver resolver, string packageName, string packageVersion)
    {
        var cachedPaths = resolver.ResolveAll();

        return cachedPaths
            .Select(cache => Path.Combine(cache, packageName, packageVersion))
            .FirstOrDefault(fileSystem.IsDirectoryExists);
    }

    private async Task<string?> TryDownloadPackageAsync(string packageName, string packageVersion, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(packageName) || string.IsNullOrEmpty(packageVersion))
            return null;
        
        var tempPath = Path.GetTempPath();
        var packagePath = Path.Combine(tempPath, packageName, packageVersion);
        
        if (fileSystem.IsDirectoryExists(packagePath))
            return packagePath;
        
        try
        {
            return await retrievalService.DownloadPackageAsync(packageName, packageVersion, packagePath, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private static Dictionary<string, string?> BuildMetadata(NuGetLicense? license, NuGetResource? commonResources)
    {
        return new Dictionary<string, string?>
        {
            [nameof(NuGetLicense.LicenseUrl)] = license?.LicenseUrl,
            [nameof(NuGetLicense.LicenseContent)] = license?.LicenseContent,
            [nameof(NuGetLicense.License)] = license?.License,
            [nameof(NuGetResource.ProjectUrl)] = commonResources?.ProjectUrl,
            [nameof(NuGetResource.Title)] = commonResources?.Title,
            [nameof(NuGetResource.Authors)] = commonResources?.Authors,
            [nameof(NuGetResource.Owners)] = commonResources?.Owners,
            [nameof(NuGetResource.RequireLicenseAcceptance)] = commonResources?.RequireLicenseAcceptance?.ToString(),
            [nameof(NuGetResource.Description)] = commonResources?.Description,
            [nameof(NuGetResource.Summary)] = commonResources?.Summary,
            [nameof(NuGetResource.ReleaseNotes)] = commonResources?.ReleaseNotes,
            [nameof(NuGetResource.Copyright)] = commonResources?.Copyright,
            [nameof(NuGetResource.Language)] = commonResources?.Language,
            [nameof(NuGetResource.Tags)] = commonResources?.Tags
        };
    }
}