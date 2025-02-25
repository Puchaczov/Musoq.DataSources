using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

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
        var commonResources = new CommonResources
        {
            PackagePath = GetPackageCachePath(nuGetCachePathResolver, packageName, packageVersion),
            PackageName = packageName,
            PackageVersion = packageVersion
        };
        
        var retrieveCommonResourcesVisitor = new RetrieveCommonResourcesVisitor(
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
            .Select(cache => Path.Combine(cache, packageName.ToLowerInvariant(), packageVersion))
            .FirstOrDefault(fileSystem.IsDirectoryExists);
    }

    private Dictionary<string, string?> BuildMetadata(ProjectLicense? license, CommonResources? commonResources)
    {
        return new Dictionary<string, string?>
        {
            ["LicenseUrl"] = license?.LicenseUrl,
            ["LicenseContent"] = license?.LicenseContent,
            ["License"] = license?.License,
            [nameof(CommonResources.ProjectUrl)] = commonResources?.ProjectUrl,
            [nameof(CommonResources.Title)] = commonResources?.Title,
            [nameof(CommonResources.Authors)] = commonResources?.Authors,
            [nameof(CommonResources.Owners)] = commonResources?.Owners,
            [nameof(CommonResources.RequireLicenseAcceptance)] = commonResources?.RequireLicenseAcceptance?.ToString(),
            [nameof(CommonResources.Description)] = commonResources?.Description,
            [nameof(CommonResources.Summary)] = commonResources?.Summary,
            [nameof(CommonResources.ReleaseNotes)] = commonResources?.ReleaseNotes,
            [nameof(CommonResources.Copyright)] = commonResources?.Copyright,
            [nameof(CommonResources.Language)] = commonResources?.Language,
            [nameof(CommonResources.Tags)] = commonResources?.Tags
        };
    }
}