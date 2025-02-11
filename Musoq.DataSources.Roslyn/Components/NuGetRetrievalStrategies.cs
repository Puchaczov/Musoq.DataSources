using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Collections.Generic;
using System.Linq;
using HtmlAgilityPack;

namespace Musoq.DataSources.Roslyn.Components;

internal static class NuGetRetrievalStrategies
{
    private static readonly HttpClient _httpClient = new();

    public static string ResolvePackagePath(
        INuGetCachePathResolver nuGetCachePathResolver,
        string packageName,
        string packageVersion)
    {
        var localPath = nuGetCachePathResolver.Resolve();
        var defaultPath = Path.Combine(localPath, packageName.ToLower(), packageVersion);

        var globalPackagesPath = GetNuGetGlobalPackagesPath();
        if (!string.IsNullOrEmpty(globalPackagesPath))
            return Path.Combine(globalPackagesPath, packageName.ToLower(), packageVersion);

        return defaultPath;
    }

    private static string? GetNuGetGlobalPackagesPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var nuGetConfigPath = Path.Combine(appData, "NuGet", "NuGet.Config");
        
        if (!File.Exists(nuGetConfigPath)) return null;

        try
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(nuGetConfigPath);

            var namespaceManager = new XmlNamespaceManager(xmlDoc.NameTable);

            var paths = new List<string>
            {
                "//configuration/configSections/sectionGroup/section[@name='repositoryPath']/@value",
                "//configuration/configSections/sectionGroup/section[@name='globalPackagesFolder']/@value",
                "//configuration/configSections/sectionGroup/section[@name='fallbackFolder']/@value",
                "//configuration/config/add[@key='repositoryPath']/@value",
                "//configuration/config/add[@key='globalPackagesFolder']/@value",
                "//configuration/config/add[@key='fallbackFolder']/@value"
            };

            foreach (var node in paths.Select(path => xmlDoc.SelectSingleNode(path, namespaceManager)))
            {
                if (node?.Value == null) continue;

                var expandedPath = Environment.ExpandEnvironmentVariables(node.Value);
                return expandedPath;
            }

            var defaultPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".nuget",
                "packages");

            return defaultPath;
        }
        catch (Exception ex)
        {
            return $"error: {ex.Message}";
        }
    }

    public static Task<string?> GetMetadataFromPathAsync(
        string packagePath,
        string packageName,
        string propertyName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var nuspecFilePath = Path.Combine(packagePath, $"{packageName}.nuspec");
        if (!File.Exists(nuspecFilePath)) return Task.FromResult<string?>(null);

        try
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(nuspecFilePath);

            var namespaceManager = new XmlNamespaceManager(xmlDoc.NameTable);

            return NuGetPackageMetadataRetriever.NuspecStrategies.TryGetValue(propertyName, out var strategy) ? 
                Task.FromResult(strategy(xmlDoc, namespaceManager)) : 
                Task.FromResult<string?>(null);
        }
        catch (Exception ex)
        {
            return Task.FromResult($"error: {ex.Message}");
        }
    }

    public static async Task<string?> GetMetadataFromWebAsync(
        string baseUrl,
        string packageName,
        string packageVersion,
        CommonResources commonResources,
        string propertyName,
        CancellationToken cancellationToken)
    {
        var url = $"{baseUrl}/{packageName}/{packageVersion}";
        if (!commonResources.TryGetHtmlDocument(url, out var doc))
        {
            try
            {
                var response = await _httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();
                var html = await response.Content.ReadAsStringAsync(cancellationToken);

                var newDoc = new HtmlDocument();
                newDoc.LoadHtml(html);
                commonResources.AddHtmlDocument(url, newDoc);

                doc = newDoc;
            }
            catch (Exception ex)
            {
                return $"error: {ex.Message}";
            }
        }

        if (NuGetPackageMetadataRetriever.HtmlStrategies.TryGetValue(propertyName, out var strategy))
            return strategy(doc);

        return null;
    }

    public static async Task<string?> GetMetadataFromCustomApiAsync(
        string apiEndpoint,
        string packageName,
        string packageVersion,
        string propertyName,
        CancellationToken cancellationToken)
    {
        var requestUrl = $"{apiEndpoint}?packageName={packageName}&packageVersion={packageVersion}&propertyName={propertyName}";
        try
        {
            using var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            // Assume the API returns a plain string
            return string.IsNullOrWhiteSpace(content) ? null : content;
        }
        catch (Exception ex)
        {
            return $"error: {ex.Message}";
        }
    }
}