using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Musoq.DataSources.Roslyn.Components;
using Musoq.DataSources.Roslyn.Components.NuGet;

namespace Musoq.DataSources.Roslyn.Services
{
    internal sealed class NuGetRetrievalService : INuGetRetrievalService
    {
        private readonly IFileSystem _fileSystem;
        private readonly IHttpClient _httpClient;

        public NuGetRetrievalService(IFileSystem fileSystem, IHttpClient httpClient)
        {
            _fileSystem = fileSystem;
            _httpClient = httpClient;
        }

        public IFileSystem FileSystem => _fileSystem;

        public string ResolvePackagePath(
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

        public Task<string?> GetMetadataFromPathAsync(
            string packagePath,
            string packageName,
            string propertyName,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var nuspecFilePath = Path.Combine(packagePath, $"{packageName}.nuspec");
            if (!_fileSystem.Exists(nuspecFilePath))
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

                var strategies = NuGetPackageMetadataRetriever.ResolveNuspecStrategies(nuspecFilePath, this);

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
            // We'll reuse the same code that the existing retriever uses to resolve HTML strategies:
            var strategies = NuGetPackageMetadataRetriever.ResolveHtmlStrategies(_httpClient);

            if (!strategies.TryGetValue(propertyName, out var pair))
            {
                // Either property is not handled by HTML or we proceed with a fallback
                if (commonResources.TryGetHtmlDocument(url, out var cachedDoc))
                    return cachedDoc is null ? null : null;
                return null;
            }

            // If we already hold the doc in memory, no need to traverse
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
                var response = await _httpClient.GetAsync(requestUrlBase, cancellationToken);
                
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

        private string? GetNuGetGlobalPackagesPath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var nuGetConfigPath = Path.Combine(appData, "NuGet", "NuGet.Config");

            if (!_fileSystem.Exists(nuGetConfigPath)) 
                return null;

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
    }
}