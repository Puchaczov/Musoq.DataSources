using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Musoq.DataSources.Roslyn.Components.NuGet.Helpers;

namespace Musoq.DataSources.Roslyn.Components.NuGet;

internal sealed class NuGetRetrievalService(IAiBasedPropertiesResolver aiBasedPropertiesResolver, IFileSystem fileSystem, IHttpClient httpClient) : INuGetRetrievalService
{
    public async Task<string?> GetMetadataFromPathAsync(
        CommonResources commonResources,
        string propertyName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        if (commonResources.PackagePath is null)
            return null;
        
        var packageName = commonResources.PackageName;

        var nuspecFilePath = Path.Combine(commonResources.PackagePath, $"{packageName}.nuspec");
        
        if (!fileSystem.IsFileExists(nuspecFilePath))
            return null;

        try
        {
            var (xmlDoc, namespaceManager) = CreateXmlDocumentAndNamespaceManager(nuspecFilePath);

            var strategies = ResolveNuspecStrategies(commonResources, aiBasedPropertiesResolver, cancellationToken);

            if (strategies.TryGetValue(propertyName, out var strategyAsync))
            {
                return await strategyAsync(xmlDoc, namespaceManager);
            }

            return null;
        }
        catch (Exception ex)
        {
            return $"error: {ex.Message}";
        }
    }

    public async Task<string?> GetMetadataFromNugetOrgAsync(
        string baseUrl,
        CommonResources commonResources,
        string propertyName,
        CancellationToken cancellationToken)
    {
        var url = $"{baseUrl}/packages/{commonResources.PackageName}/{commonResources.PackageVersion}";
        var strategies = ResolveWebScrapeStrategies(httpClient, commonResources, aiBasedPropertiesResolver, cancellationToken);

        if (!strategies.TryGetValue(propertyName, out var traverseAsync))
            return null;
        
        var retrieveAsync = await traverseAsync(url, cancellationToken);

        return await retrieveAsync();
    }

    public async Task<string?> GetMetadataFromCustomApiAsync(
        string apiEndpoint,
        CommonResources commonResources,
        string propertyName,
        CancellationToken cancellationToken)
    {
        var requestUrlBase = $"{apiEndpoint}?packageName={commonResources.PackageName}&packageVersion={commonResources.PackageVersion}&propertyName={propertyName}";
        try
        {
            var response = await httpClient.GetAsync(requestUrlBase, cancellationToken);
                
            if (response is null)
            {
                return null;
            }
                
            response.EnsureSuccessStatusCode();
                
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            return string.IsNullOrWhiteSpace(content) ? null : content;
        }
        catch (Exception ex)
        {
            return $"error: {ex.Message}";
        }
    }

    public async Task<string?> DownloadPackageAsync(string packageName, string packageVersion, string packagePath, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync($"https://www.nuget.org/api/v2/package/{packageName}/{packageVersion}", cancellationToken);
        
        if (response is null)
        {
            return null;
        }
        
        response.EnsureSuccessStatusCode();
        
        var tempPath = Path.GetTempPath();
        var tempFilePath = Path.Combine(tempPath, $"{packageName}.{packageVersion}.nupkg");
        
        await using var fileStream = await fileSystem.CreateFileAsync(tempFilePath);
        
        await response.Content.CopyToAsync(fileStream, cancellationToken);
        
        await fileSystem.ExtractZipAsync(tempFilePath, packagePath, cancellationToken);
        
        return packagePath;
    }

    private static IReadOnlyDictionary<string, Func<XmlDocument, XmlNamespaceManager, Task<string?>>> ResolveNuspecStrategies(CommonResources commonResources, IAiBasedPropertiesResolver aiBasedPropertiesResolver, CancellationToken cancellationToken)
    {
        return new Dictionary<string, Func<XmlDocument, XmlNamespaceManager, Task<string?>>>
        {
            [nameof(CommonResources.ProjectUrl)] = NuspecHelpers.GetProjectUrlFromNuspecAsync,
            [nameof(CommonResources.Title)] = NuspecHelpers.GetTitleFromNuspecAsync,
            [nameof(CommonResources.Authors)] = NuspecHelpers.GetAuthorsFromNuspecAsync,
            [nameof(CommonResources.Owners)] = NuspecHelpers.GetOwnersFromNuspecAsync,
            [nameof(CommonResources.RequireLicenseAcceptance)] = NuspecHelpers.GetRequireLicenseAcceptanceFromNuspecAsync,
            [nameof(CommonResources.Description)] = NuspecHelpers.GetDescriptionFromNuspecAsync,
            [nameof(CommonResources.Summary)] = NuspecHelpers.GetSummaryFromNuspecAsync,
            [nameof(CommonResources.ReleaseNotes)] = NuspecHelpers.GetReleaseNotesFromNuspecAsync,
            [nameof(CommonResources.Copyright)] = NuspecHelpers.GetCopyrightFromNuspecAsync,
            [nameof(CommonResources.Language)] = NuspecHelpers.GetLanguageFromNuspecAsync,
            [nameof(CommonResources.Tags)] = NuspecHelpers.GetTagsFromNuspecAsync,
            ["LicensesNames"] = (doc, manager) => NuspecHelpers.GetLicensesNamesFromNuspecAsync(doc, manager, commonResources, aiBasedPropertiesResolver, cancellationToken),
            ["LicenseUrl"] = NuspecHelpers.GetLicenseUrlFromNuspecAsync,
            ["LicenseContent"] = (doc, manager) => NuspecHelpers.GetLicenseContentFromNuspecAsync(doc, manager, commonResources, cancellationToken)
        };
    }
        
    private static IReadOnlyDictionary<string, Func<string, CancellationToken, Task<Func<Task<string?>>>>> ResolveWebScrapeStrategies(IHttpClient client, CommonResources commonResources, IAiBasedPropertiesResolver aiBasedPropertiesResolver, CancellationToken cancellationToken)
    {
        var capturedClient = client;
        return new Dictionary<string, Func<string, CancellationToken, Task<Func<Task<string?>>>>>
        {
            ["LicensesNames"] = async (url, token) => await NugetHelpers.DiscoverLicensesNamesAsync(url, commonResources, capturedClient, aiBasedPropertiesResolver, token),
            ["LicenseUrl"] = async (url, token) => await NugetHelpers.DiscoverLicenseUrlAsync(url, commonResources, capturedClient, aiBasedPropertiesResolver, token),
            ["LicenseContent"] = async (url, _) => await NugetHelpers.DiscoverLicenseContentAsync(url, commonResources, capturedClient, aiBasedPropertiesResolver, cancellationToken)
        };
    }

    private static (XmlDocument, XmlNamespaceManager) CreateXmlDocumentAndNamespaceManager(string nuspecFilePath)
    {
        var xmlDoc = new XmlDocument();
        xmlDoc.Load(nuspecFilePath);

        var namespaceManager = new XmlNamespaceManager(xmlDoc.NameTable);
        var nugetNamespace = xmlDoc.DocumentElement?.NamespaceURI ?? string.Empty;
        if (!string.IsNullOrEmpty(nugetNamespace))
        {
            namespaceManager.AddNamespace("nu", nugetNamespace);
        }

        return (xmlDoc, namespaceManager);
    }
}