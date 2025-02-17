using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace Musoq.DataSources.Roslyn.Components.NuGet;

internal sealed class NuGetRetrievalService(IFileSystem fileSystem, IHttpClient httpClient) : INuGetRetrievalService
{
    public Task<string?> GetMetadataFromPathAsync(
        string packagePath,
        string packageName,
        string propertyName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var nuspecFilePath = Path.Combine(packagePath, $"{packageName}.nuspec");
        if (!fileSystem.IsFileExists(nuspecFilePath))
            return Task.FromResult<string?>(null);

        try
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(nuspecFilePath);

            var namespaceManager = new XmlNamespaceManager(xmlDoc.NameTable);
            var nugetNamespace = xmlDoc.DocumentElement?.NamespaceURI ?? string.Empty;
            if (!string.IsNullOrEmpty(nugetNamespace))
            {
                namespaceManager.AddNamespace("nu", nugetNamespace);
            }

            var strategies = NuGetPackageMetadataRetriever.ResolveNuspecStrategies(nuspecFilePath, fileSystem);

            return Task.FromResult(
                strategies.TryGetValue(propertyName, out var strategy) 
                    ? strategy(xmlDoc, namespaceManager) 
                    : null
            );
        }
        catch (Exception ex)
        {
            return Task.FromResult<string?>($"error: {ex.Message}");
        }
    }

    public async Task<string?> GetMetadataFromWebAsync(
        string baseUrl,
        string packageName,
        string packageVersion,
        CommonResources commonResources,
        string propertyName,
        CancellationToken cancellationToken)
    {
        var url = $"{baseUrl}/{packageName}/{packageVersion}";
        var strategies = NuGetPackageMetadataRetriever.ResolveHtmlStrategies(httpClient);

        if (!strategies.TryGetValue(propertyName, out var pair))
        {
            return null;
        }

        if (commonResources.TryGetHtmlDocument(url, out var doc))
            return doc is null ? null : pair.Retrieve(doc);

        try
        {
            doc = await pair.TraverseAsync(url, cancellationToken);
            commonResources.AddHtmlDocument(url, doc);
        }
        catch (Exception ex)
        {
            return $"error: {ex.Message}";
        }

        return pair.Retrieve(doc);
    }

    public async Task<string?> GetMetadataFromCustomApiAsync(
        string apiEndpoint,
        string packageName,
        string packageVersion,
        string propertyName,
        CancellationToken cancellationToken)
    {
        var requestUrlBase = $"{apiEndpoint}?packageName={packageName}&packageVersion={packageVersion}&propertyName={propertyName}";
        try
        {
            var response = await httpClient.GetAsync(requestUrlBase, cancellationToken);
                
            if (response is null)
            {
                return null;
            }
                
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