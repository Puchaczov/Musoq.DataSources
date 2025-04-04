using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Musoq.DataSources.Roslyn.Components.NuGet.Helpers;

namespace Musoq.DataSources.Roslyn.Components.NuGet;

internal sealed class NuGetRetrievalService(INuGetPropertiesResolver nuGetPropertiesResolver, IFileSystem fileSystem, IHttpClient httpClient) : INuGetRetrievalService
{
    public async Task<string?> GetMetadataFromPathAsync(
        NuGetResource commonResources,
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
            var (xmlDoc, namespaceManager) = await CreateXmlDocumentAndNamespaceManager(nuspecFilePath, fileSystem, cancellationToken);

            var strategies = ResolveNuspecStrategies(commonResources, nuGetPropertiesResolver, cancellationToken);

            if (strategies.TryGetValue(propertyName, out var strategyAsync))
            {
                return await strategyAsync(xmlDoc, namespaceManager);
            }

            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<string?> GetMetadataFromNugetOrgAsync(
        string baseUrl,
        NuGetResource commonResources,
        string propertyName,
        CancellationToken cancellationToken)
    {
        try
        {
            var url = $"{baseUrl}/packages/{commonResources.PackageName}/{commonResources.PackageVersion}";
            var strategies = ResolveWebScrapeStrategies(httpClient, commonResources, nuGetPropertiesResolver, cancellationToken);

            if (!strategies.TryGetValue(propertyName, out var traverseAsync))
                return null;
        
            var retrieveAsync = await traverseAsync(url, cancellationToken);

            return await retrieveAsync();
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<string?> GetMetadataFromCustomApiAsync(
        string apiEndpoint,
        NuGetResource commonResources,
        string propertyName,
        CancellationToken cancellationToken)
    {
        var requestUrlBase = $"{apiEndpoint}?packageName={commonResources.PackageName}&packageVersion={commonResources.PackageVersion}&propertyName={propertyName}";
        var response = await httpClient.GetAsync(requestUrlBase, cancellationToken);
                
        if (response is null)
        {
            return null;
        }
                
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }
                
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(content) ? null : content;
    }

    public async Task<string?> DownloadPackageAsync(string packageName, string packageVersion, string packagePath, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync($"https://www.nuget.org/api/v2/package/{packageName}/{packageVersion}", cancellationToken);
        
        if (response is null)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }
        
        var tempPath = Path.GetTempPath();
        var tempFilePath = Path.Combine(tempPath, $"{packageName}.{packageVersion}.nupkg");
        
        await using (var fileStream = await fileSystem.CreateFileAsync(tempFilePath))
        {
            await response.Content.CopyToAsync(fileStream, cancellationToken);
        }
        
        await fileSystem.ExtractZipAsync(tempFilePath, packagePath, cancellationToken);
        
        return packagePath;
    }

    private static IReadOnlyDictionary<string, Func<XmlDocument, XmlNamespaceManager, Task<string?>>> ResolveNuspecStrategies(NuGetResource commonResources, INuGetPropertiesResolver aiBasedPropertiesResolver, CancellationToken cancellationToken)
    {
        return new Dictionary<string, Func<XmlDocument, XmlNamespaceManager, Task<string?>>>
        {
            [nameof(NuGetResource.ProjectUrl)] = NuspecHelpers.GetProjectUrlFromNuspecAsync,
            [nameof(NuGetResource.Title)] = NuspecHelpers.GetTitleFromNuspecAsync,
            [nameof(NuGetResource.Authors)] = NuspecHelpers.GetAuthorsFromNuspecAsync,
            [nameof(NuGetResource.Owners)] = NuspecHelpers.GetOwnersFromNuspecAsync,
            [nameof(NuGetResource.RequireLicenseAcceptance)] = NuspecHelpers.GetRequireLicenseAcceptanceFromNuspecAsync,
            [nameof(NuGetResource.Description)] = NuspecHelpers.GetDescriptionFromNuspecAsync,
            [nameof(NuGetResource.Summary)] = NuspecHelpers.GetSummaryFromNuspecAsync,
            [nameof(NuGetResource.ReleaseNotes)] = NuspecHelpers.GetReleaseNotesFromNuspecAsync,
            [nameof(NuGetResource.Copyright)] = NuspecHelpers.GetCopyrightFromNuspecAsync,
            [nameof(NuGetResource.Language)] = NuspecHelpers.GetLanguageFromNuspecAsync,
            [nameof(NuGetResource.Tags)] = NuspecHelpers.GetTagsFromNuspecAsync,
            ["LicensesNames"] = (doc, manager) => NuspecHelpers.GetLicensesNamesFromNuspecAsync(doc, manager, commonResources, aiBasedPropertiesResolver, cancellationToken),
            [nameof(NuGetLicense.LicenseUrl)] = NuspecHelpers.GetLicenseUrlFromNuspecAsync,
            [nameof(NuGetLicense.LicenseContent)] = (doc, manager) => NuspecHelpers.GetLicenseContentFromNuspecAsync(doc, manager, commonResources, cancellationToken)
        };
    }
        
    private static IReadOnlyDictionary<string, Func<string, CancellationToken, Task<Func<Task<string?>>>>> ResolveWebScrapeStrategies(IHttpClient client, NuGetResource commonResources, INuGetPropertiesResolver aiBasedPropertiesResolver, CancellationToken cancellationToken)
    {
        var capturedClient = client;
        return new Dictionary<string, Func<string, CancellationToken, Task<Func<Task<string?>>>>>
        {
            ["LicensesNames"] = async (url, token) => await NugetHelpers.DiscoverLicensesNamesAsync(url, commonResources, capturedClient, aiBasedPropertiesResolver, token),
            [nameof(NuGetLicense.LicenseUrl)] = async (url, token) => await NugetHelpers.DiscoverLicenseUrlAsync(url, commonResources, capturedClient, aiBasedPropertiesResolver, token),
            [nameof(NuGetLicense.LicenseContent)] = async (url, _) => await NugetHelpers.DiscoverLicenseContentAsync(url, commonResources, capturedClient, aiBasedPropertiesResolver, cancellationToken)
        };
    }

    private static async Task<(XmlDocument, XmlNamespaceManager)> CreateXmlDocumentAndNamespaceManager(string nuspecFilePath, IFileSystem fileSystem, CancellationToken cancellationToken)
    {
        var fileContent = await fileSystem.ReadAllTextAsync(nuspecFilePath, cancellationToken);
        var xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(fileContent);

        var namespaceManager = new XmlNamespaceManager(xmlDoc.NameTable);
        var nugetNamespace = xmlDoc.DocumentElement?.NamespaceURI ?? string.Empty;
        if (!string.IsNullOrEmpty(nugetNamespace))
        {
            namespaceManager.AddNamespace("nu", nugetNamespace);
        }

        return (xmlDoc, namespaceManager);
    }
}