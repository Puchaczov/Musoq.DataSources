using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.IO;
using System.Linq;

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
    public async Task<IReadOnlyDictionary<string, string?>> GetMetadataAsync(
        string packageName,
        string packageVersion,
        CancellationToken cancellationToken)
    {
        var commonResources = new CommonResources
        {
            PackagePath = GetPackageCachePath(nuGetCachePathResolver, packageName, packageVersion)
        };

        var propertyNames = new List<string>
        {
            nameof(CommonResources.LicenseUrl),
            nameof(CommonResources.License),
            nameof(CommonResources.ProjectUrl),
            nameof(CommonResources.Title),
            nameof(CommonResources.Authors),
            nameof(CommonResources.Owners),
            nameof(CommonResources.RequireLicenseAcceptance),
            nameof(CommonResources.Description),
            nameof(CommonResources.Summary),
            nameof(CommonResources.ReleaseNotes),
            nameof(CommonResources.Copyright),
            nameof(CommonResources.Language),
            nameof(CommonResources.Tags),
            nameof(CommonResources.LicenseContent)
        };

        foreach (var propertyName in propertyNames)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var propertyInfo = typeof(CommonResources).GetProperty(propertyName);
            if (propertyInfo == null) continue;

            string? localValue = null;

            if (commonResources.PackagePath is not null)
            {
                localValue = await retrievalService.GetMetadataFromPathAsync(
                    commonResources.PackagePath,
                    packageName,
                    propertyName,
                    cancellationToken);
            }

            var webValue = localValue ?? await retrievalService.GetMetadataFromWebAsync(
                "https://www.nuget.org/packages",
                packageName,
                packageVersion,
                commonResources,
                propertyName,
                cancellationToken);

            var resolvedValue = webValue;
            if (resolvedValue == null && !string.IsNullOrEmpty(customApiEndpoint))
            {
                try
                {
                    resolvedValue = await retrievalService.GetMetadataFromCustomApiAsync(
                        customApiEndpoint!,
                        packageName,
                        packageVersion,
                        propertyName,
                        cancellationToken);
                }
                catch (HttpRequestException)
                {
                    resolvedValue = null;
                }
            }

            var targetType = propertyInfo.PropertyType;
            if (targetType == typeof(bool?))
            {
                bool? parsedBool = null;
                if (bool.TryParse(resolvedValue, out var tmp))
                    parsedBool = tmp;
                propertyInfo.SetValue(commonResources, parsedBool);
            }
            else
            {
                propertyInfo.SetValue(commonResources, resolvedValue);
            }
        }

        var result = new Dictionary<string, string?>
        {
            [nameof(CommonResources.LicenseUrl)] = commonResources.LicenseUrl,
            [nameof(CommonResources.License)] = commonResources.License,
            [nameof(CommonResources.ProjectUrl)] = commonResources.ProjectUrl,
            [nameof(CommonResources.Title)] = commonResources.Title,
            [nameof(CommonResources.Authors)] = commonResources.Authors,
            [nameof(CommonResources.Owners)] = commonResources.Owners,
            [nameof(CommonResources.RequireLicenseAcceptance)] = commonResources.RequireLicenseAcceptance?.ToString(),
            [nameof(CommonResources.Description)] = commonResources.Description,
            [nameof(CommonResources.Summary)] = commonResources.Summary,
            [nameof(CommonResources.ReleaseNotes)] = commonResources.ReleaseNotes,
            [nameof(CommonResources.Copyright)] = commonResources.Copyright,
            [nameof(CommonResources.Language)] = commonResources.Language,
            [nameof(CommonResources.Tags)] = commonResources.Tags,
            [nameof(CommonResources.LicenseContent)] = commonResources.LicenseContent
        };

        return result;
    }
        
    private string? GetPackageCachePath(INuGetCachePathResolver resolver, string packageName, string packageVersion)
    {
        var cachedPaths = resolver.ResolveAll();
        
        foreach (var cache in cachedPaths)
        {
            var packagePath = Path.Combine(cache, packageName.ToLowerInvariant(), packageVersion);
            
            if (fileSystem.IsDirectoryExists(packagePath))
                return packagePath;
        }

        return null;
    }
        
    internal static IReadOnlyDictionary<string, TraverseRetrievePair> ResolveHtmlStrategies(IHttpClient client)
    {
        var capturedClient = client;
        return new Dictionary<string, TraverseRetrievePair>
        {
            [nameof(CommonResources.LicenseUrl)] = new(
                async (url, token) => await NuGetMetadataStrategies.TraverseToLicenseUrlAsync(url, capturedClient, token),
                NuspecHelpers.GetLicenseUrlFromHtml),
            [nameof(CommonResources.ProjectUrl)] = new(
                async (url, token) => await NuGetMetadataStrategies.TraverseToProjectUrlAsync(url, capturedClient, token),
                NuspecHelpers.GetProjectUrlFromHtml),
            [nameof(CommonResources.LicenseContent)] = new(
                async (url, token) => await NuGetMetadataStrategies.TraverseToLicenseContentAsync(url, capturedClient, token),
                NuspecHelpers.GetLicenseContentFromHtml),
            [nameof(CommonResources.License)] = new(
                async (url, token) => await NuGetMetadataStrategies.TraverseToLicenseAsync(url, capturedClient, token),
                NuspecHelpers.GetLicenseFromHtml)
        };
    }
        
    internal static IReadOnlyDictionary<string, Func<XmlDocument, XmlNamespaceManager, string?>> ResolveNuspecStrategies(string path, IFileSystem fileSystem)
    {
        return new Dictionary<string, Func<XmlDocument, XmlNamespaceManager, string?>>
        {
            [nameof(CommonResources.LicenseUrl)] = NuspecHelpers.GetLicenseUrlFromNuspec,
            [nameof(CommonResources.License)] = NuspecHelpers.GetLicenseFromNuspec,
            [nameof(CommonResources.ProjectUrl)] = NuspecHelpers.GetProjectUrlFromNuspec,
            [nameof(CommonResources.Title)] = NuspecHelpers.GetTitleFromNuspec,
            [nameof(CommonResources.Authors)] = NuspecHelpers.GetAuthorsFromNuspec,
            [nameof(CommonResources.Owners)] = NuspecHelpers.GetOwnersFromNuspec,
            [nameof(CommonResources.RequireLicenseAcceptance)] = NuspecHelpers.GetRequireLicenseAcceptanceFromNuspec,
            [nameof(CommonResources.Description)] = NuspecHelpers.GetDescriptionFromNuspec,
            [nameof(CommonResources.Summary)] = NuspecHelpers.GetSummaryFromNuspec,
            [nameof(CommonResources.ReleaseNotes)] = NuspecHelpers.GetReleaseNotesFromNuspec,
            [nameof(CommonResources.Copyright)] = NuspecHelpers.GetCopyrightFromNuspec,
            [nameof(CommonResources.Language)] = NuspecHelpers.GetLanguageFromNuspec,
            [nameof(CommonResources.Tags)] = NuspecHelpers.GetTagsFromNuspec,
            [nameof(CommonResources.LicenseContent)] = (document, manager) =>
            {
                var strategy = new NuspecHelpers(path, fileSystem);
                return strategy.GetLicenseContentFromNuspec(document, manager);
            }
        };
    }
}