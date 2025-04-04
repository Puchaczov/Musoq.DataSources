using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Musoq.DataSources.Roslyn.Components.NuGet;

internal sealed class NuGetPackageMetadataRetriever(
    INuGetCachePathResolver nuGetCachePathResolver,
    string? customApiEndpoint,
    INuGetRetrievalService retrievalService,
    IFileSystem fileSystem,
    IPackageVersionConcurrencyManager packageVersionConcurrencyManager, 
    ILogger logger)
    : INuGetPackageMetadataRetriever
{
    private readonly ConcurrentDictionary<(string, string), List<Dictionary<string, string?>>> _packageMetadataCache = new();
    
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
        
        logger.LogTrace($"Retrieving metadata for {packageName} {packageVersion}");
        
        if (packageVersion.Length == 0)
        {
            logger.LogTrace($"Package version is empty for {packageName}");
            yield break;
        }
        
        if (packageVersion[0] == '[' && packageVersion[^1] == ']')
        {
            packageVersion = packageVersion[1..^1];
        }
        
        if (string.IsNullOrEmpty(packageName) || string.IsNullOrEmpty(packageVersion))
        {
            logger.LogTrace($"Package name or version is empty for {packageName} {packageVersion}");
            yield break;
        }
        
        if (!Version.TryParse(packageVersion, out _))
        {
            logger.LogTrace($"Package version is not valid for {packageName} {packageVersion}");
            yield break;
        }
        
        if (_packageMetadataCache.TryGetValue((packageName, packageVersion), out var cachedMetadata))
        {
            logger.LogTrace($"Using cached metadata for {packageName} {packageVersion}");
            
            foreach (var cachedRow in cachedMetadata)
            {
                yield return cachedRow;
            }
            
            yield break;
        }

        using var @lock = await packageVersionConcurrencyManager.AcquireLockAsync(packageName, packageVersion, cancellationToken);
        
        if (_packageMetadataCache.TryGetValue((packageName, packageVersion), out cachedMetadata))
        {
            logger.LogTrace($"Using cached metadata for {packageName} {packageVersion}");
            
            foreach (var cachedRow in cachedMetadata)
            {
                yield return cachedRow;
            }
            
            yield break;
        }
        
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
        var metadata = new List<Dictionary<string, string?>>();
        
        Dictionary<string, string?> row;

        if (licenses.Length == 0)
        {
            logger.LogTrace($"No licenses found for {packageName} {packageVersion}");

            row = BuildMetadata(null, commonResources);
            
            metadata.Add(row);
            
            yield return row;
        }
        else
        {
            logger.LogTrace($"Found {licenses.Length} licenses for {packageName} {packageVersion}");

            row = BuildMetadata(licenses[0], commonResources);
            
            metadata.Add(row);
            
            yield return row;
            
            for (var i = 1; i < licenses.Length; i++)
            {
                row = BuildMetadata(licenses[i], commonResources);
                
                metadata.Add(row);
                
                yield return row;
            }
        }
        
        _packageMetadataCache.AddOrUpdate((packageName, packageVersion),
            metadata,
            (_, _) => metadata);
    }

    private string? GetPackageCachePath(INuGetCachePathResolver resolver, string packageName, string packageVersion)
    {
        var cachedPaths = resolver.ResolveAll();

        var packagePath = cachedPaths
            .Select(cache => Path.Combine(cache, packageName, packageVersion))
            .FirstOrDefault(fileSystem.IsDirectoryExists);
        
        logger.LogTrace($"Package cache used: {packagePath} for {packageName} {packageVersion}");
        
        return packagePath;
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
            logger.LogTrace($"Package downloader used: {packagePath} for {packageName} {packageVersion}");
            
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