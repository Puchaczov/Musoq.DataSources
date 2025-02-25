using System;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace Musoq.DataSources.Roslyn.Components.NuGet.Helpers;

internal static class NugetHelpers
{
    public static async Task<HtmlDocument> DiscoverLicenseUrlAsync(string url, CommonResources commonResources, IHttpClient httpClient, CancellationToken cancellationToken)
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
        
        return newDoc;
    }
    
    public static async Task<HtmlDocument> DiscoverLicenseContentAsync(string url, CommonResources commonResources, IHttpClient httpClient, CancellationToken cancellationToken)
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
        
        var licenseUrl = ExtractUrl(newDoc, "//a[@data-tract='outbound-license-url']", "href");
        
        if (licenseUrl is null)
        {
            throw new InvalidOperationException("Failed to extract license url");
        }

        response = await httpClient.GetAsync(licenseUrl, cancellationToken);
        
        if (response is null)
        {
            throw new InvalidOperationException($"Failed to retrieve {licenseUrl}");
        }
        
        response.EnsureSuccessStatusCode();
        
        html = await response.Content.ReadAsStringAsync(cancellationToken);
        newDoc.LoadHtml(html);
        
        return newDoc;
    }
    
    public static async Task<HtmlDocument> DiscoverLicensesNamesAsync(string url, CommonResources commonResources, IHttpClient httpClient, CancellationToken cancellationToken)
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
            
        return newDoc;
    }
    
    public static async Task<string?> GetLicensesNamesFromHtmlAsync(HtmlDocument doc)
    {
        return await Task.FromResult(doc.DocumentNode.SelectSingleNode("//div[@id='licenses']")?.InnerText);
    }
    
    public static async Task<string?> GetLicenseUrlFromHtmlAsync(HtmlDocument doc)
    {
        return await Task.FromResult(ExtractUrl(doc, "//a[@id='licenseUrl']", "href"));
    }

    public static async Task<string?> GetLicenseContentFromHtmlAsync(HtmlDocument doc)
    {
        return await Task.FromResult(doc.DocumentNode.SelectSingleNode("//div[@id='licenseContent']")?.InnerText);
    }

    private static string? ExtractUrl(HtmlDocument doc, string xpath, string attributeName)
    {
        var node = doc.DocumentNode.SelectSingleNode(xpath);
        return node?.Attributes[attributeName]?.Value;
    }
}