using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Musoq.DataSources.Roslyn.Components.NuGet.Helpers;

namespace Musoq.DataSources.Roslyn.Components.NuGet;

internal sealed class NuGetRetrievalService(IFileSystem fileSystem, IHttpClient httpClient) : INuGetRetrievalService
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

            var strategies = ResolveNuspecStrategies(nuspecFilePath, fileSystem, httpClient, cancellationToken);

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

    public async Task<string?> GetMetadataFromWebAsync(
        string baseUrl,
        CommonResources commonResources,
        string propertyName,
        CancellationToken cancellationToken)
    {
        var url = $"{baseUrl}/packages/{commonResources.PackageName}/{commonResources.PackageVersion}";
        var strategies = ResolveHtmlStrategies(httpClient, commonResources, cancellationToken);

        if (!strategies.TryGetValue(propertyName, out var pair))
            return null;

        if (commonResources.TryGetHtmlDocument(url, out var doc))
            return doc is null ? null : await pair.RetrieveAsync(doc);

        try
        {
            doc = await pair.TraverseAsync(url, cancellationToken);
            commonResources.AddHtmlDocument(url, doc);
        }
        catch (Exception ex)
        {
            return $"error: {ex.Message}";
        }

        return await pair.RetrieveAsync(doc);
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
        
    private static IReadOnlyDictionary<string, Func<XmlDocument, XmlNamespaceManager, Task<string?>>> ResolveNuspecStrategies(string packagePath, IFileSystem fileSystem, IHttpClient client, CancellationToken cancellationToken)
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
            ["LicensesNames"] = NuspecHelpers.GetLicensesNamesFromNuspecAsync,
            ["LicenseUrl"] = NuspecHelpers.GetLicenseUrlFromNuspecAsync,
            ["LicenseContent"] = NuspecHelpers.GetLicenseContentFromNuspecAsync
        };
    }
        
    private static IReadOnlyDictionary<string, TraverseRetrievePair> ResolveHtmlStrategies(IHttpClient client, CommonResources commonResources, CancellationToken cancellationToken)
    {
        var capturedClient = client;
        return new Dictionary<string, TraverseRetrievePair>
        {
            ["LicensesNames"] = new(
                async (url, _) => await NugetHelpers.DiscoverLicensesNamesAsync(url, commonResources, capturedClient, cancellationToken),
                NugetHelpers.GetLicensesNamesFromHtmlAsync),
            ["LicenseUrl"] = new(
                async (url, _) => await NugetHelpers.DiscoverLicenseUrlAsync(url, commonResources, capturedClient, cancellationToken),
                NugetHelpers.GetLicenseUrlFromHtmlAsync),
            ["LicenseContent"] = new(
                async (url, _) => await NugetHelpers.DiscoverLicenseContentAsync(url, commonResources, capturedClient, cancellationToken),
                NugetHelpers.GetLicenseContentFromHtmlAsync)
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