using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace Musoq.DataSources.Roslyn.Components.NuGet.Helpers;

internal static class NugetHelpers
{
    public static async Task<Func<Task<string?>>> DiscoverLicensesNamesAsync(string url, CommonResources commonResources, IHttpClient httpClient, IAiBasedPropertiesResolver aiBasedPropertiesResolver, CancellationToken cancellationToken)
    {
        if (!commonResources.TryGetHtmlDocument(url, out var htmlDocument))
        {
            var response = await httpClient.GetAsync(url, cancellationToken);
        
            if (response is null)
            {
                throw new InvalidOperationException($"Failed to retrieve {url}");
            }
        
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync(cancellationToken);

            var newDoc = new HtmlDocument();
            newDoc.LoadHtml(html);
        }

        if (htmlDocument is null)
        {
            return () => Task.FromResult<string?>(null);
        }
        
        //License info is available on the page
        if (TryExtractUrl(htmlDocument, "//a[@data-tract='outbound-license-url']", "href", out var licenseUrl) && licenseUrl is not null)
        {
            return async () => System.Text.Json.JsonSerializer.Serialize(await aiBasedPropertiesResolver.GetLicenseNamesByLicenseUrlAsync(licenseUrl, cancellationToken));
        }
        
        //Source repository is available on the page
        if (TryExtractUrl(htmlDocument, "//a[@data-tract='outbound-repository-url']", "href", out var sourceRepositoryUrl) && sourceRepositoryUrl is not null)
        {
            return async () => System.Text.Json.JsonSerializer.Serialize(await aiBasedPropertiesResolver.GetLicenseNamesBySourceRepositoryUrlAsync(sourceRepositoryUrl, cancellationToken));
        }
            
        return () => Task.FromResult<string?>(null);
    }
    
    public static async Task<Func<Task<string?>>> DiscoverLicenseUrlAsync(string url, CommonResources commonResources, IHttpClient httpClient, IAiBasedPropertiesResolver aiBasedPropertiesResolver, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync(url, cancellationToken);
        
        if (response is null)
        {
            throw new InvalidOperationException($"Failed to retrieve {url}");
        }
        
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(cancellationToken);

        var newDoc = new HtmlDocument();
        newDoc.LoadHtml(html);
        
        if (TryExtractUrl(newDoc, "//a[@data-tract='outbound-license-url']", "href", out var licenseUrl) && licenseUrl is not null)
        {
            return () => Task.FromResult<string?>(licenseUrl);
        }
        
        if (TryExtractUrl(newDoc, "//a[@data-tract='outbound-repository-url']", "href", out var sourceRepositoryUrl) && sourceRepositoryUrl is not null)
        {
            var resolvedLicenseUrl = await FindLicenseUrlAsync(sourceRepositoryUrl, httpClient, cancellationToken);
            return () => Task.FromResult(resolvedLicenseUrl);
        }
        
        return () => Task.FromResult<string?>(null);
    }
    
    public static async Task<Func<Task<string?>>> DiscoverLicenseContentAsync(string url, CommonResources commonResources, IHttpClient httpClient, IAiBasedPropertiesResolver aiBasedPropertiesResolver, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync(url, cancellationToken);
        
        if (response is null)
        {
            throw new InvalidOperationException($"Failed to retrieve {url}");
        }
        
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(cancellationToken);

        var newDoc = new HtmlDocument();
        newDoc.LoadHtml(html);
        
        if (TryExtractUrl(newDoc, "//a[@data-tract='outbound-license-url']", "href", out var licenseUrl) && licenseUrl is not null)
        {
            return async () =>
            {
                var httpResponseMessage = await httpClient.GetAsync(licenseUrl, cancellationToken);
                
                return httpResponseMessage is not null ? await httpResponseMessage.Content.ReadAsStringAsync(cancellationToken) : null;
            };
        }
        
        if (TryExtractUrl(newDoc, "//a[@data-tract='outbound-repository-url']", "href", out var sourceRepositoryUrl) && sourceRepositoryUrl is not null)
        {
            return async () =>
            {
                var resolvedLicenseUrl = await FindLicenseUrlAsync(sourceRepositoryUrl, httpClient, cancellationToken);
                
                if (resolvedLicenseUrl is null)
                {
                    return null;
                }
                
                var httpResponseMessage = await httpClient.GetAsync(resolvedLicenseUrl, cancellationToken);
                
                return httpResponseMessage is not null ? await httpResponseMessage.Content.ReadAsStringAsync(cancellationToken) : null;
            };
        }
        
        return () => Task.FromResult<string?>(null);
    }

    private static async Task<string?> FindLicenseUrlAsync(string sourceRepositoryUrl, IHttpClient httpClient, CancellationToken cancellationToken)
    {
        sourceRepositoryUrl = sourceRepositoryUrl.TrimEnd('/');
        
        var licenseFileNames = new[]
        {
            "LICENSE",
            "LICENSE.md",
            "LICENSE.txt",
            "COPYING",
            "COPYING.md",
            "COPYING.txt",
            "LICENSE.MIT",
            "LICENSE.APACHE",
            "LICENSE.BSD"
        };
        
        if (sourceRepositoryUrl.Contains("github.com"))
        {
            var rawUrl = sourceRepositoryUrl
                .Replace("github.com", "raw.githubusercontent.com")
                .Replace("https://raw.githubusercontent.com/", "https://raw.githubusercontent.com/");
                
            if (!rawUrl.EndsWith("/"))
                rawUrl += "/";
                
            if (!rawUrl.Contains("/master/") && !rawUrl.Contains("/main/"))
                rawUrl += "master/";
                
            foreach (var fileName in licenseFileNames)
            {
                var licenseUrl = rawUrl + fileName;
                var response = await httpClient.GetAsync(licenseUrl, cancellationToken);
                
                if (response is { IsSuccessStatusCode: true })
                {
                    return licenseUrl;
                }
            }
        }
        
        else if (sourceRepositoryUrl.Contains("gitlab.com"))
        {
            var rawUrl = sourceRepositoryUrl;
            
            if (!rawUrl.Contains("/-/raw/"))
                rawUrl += "/-/raw/master/";
                
            foreach (var fileName in licenseFileNames)
            {
                var licenseUrl = rawUrl + fileName;
                var response = await httpClient.GetAsync(licenseUrl, cancellationToken);
                
                if (response is { IsSuccessStatusCode: true })
                {
                    return licenseUrl;
                }
            }
        }
        
        return null;
    }

    private static bool TryExtractUrl(HtmlDocument doc, string xpath, string attributeName, out string? url)
    {
        var node = doc.DocumentNode.SelectSingleNode(xpath);
        
        if (node is null)
        {
            url = null;
            return false;
        }
        
        url = node.Attributes[attributeName]?.Value;
        return true;
    }
}