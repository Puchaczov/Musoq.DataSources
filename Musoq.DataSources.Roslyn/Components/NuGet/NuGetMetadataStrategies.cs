using System;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace Musoq.DataSources.Roslyn.Components.NuGet;

internal abstract class NuGetMetadataStrategies
{
    public static async Task<HtmlDocument> TraverseToLicenseUrlAsync(string url, IHttpClient httpClient, CancellationToken cancellationToken)
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
        
    public static async Task<HtmlDocument> TraverseToProjectUrlAsync(string url, IHttpClient httpClient, CancellationToken cancellationToken)
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
        
    public static async Task<HtmlDocument> TraverseToLicenseContentAsync(string url, IHttpClient httpClient, CancellationToken cancellationToken)
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
            
        // Find with xpath, element "a" that link points to https://licenses.nuget.org/* and get its href attribute
        var licenseUrl = newDoc.DocumentNode.SelectSingleNode("//a[contains(@href, 'https://licenses.nuget.org')]")?.Attributes["href"]?.Value;

        if (licenseUrl is not null)
        {
            var licenseResponse = await httpClient.GetAsync(licenseUrl, cancellationToken);
            if (licenseResponse is null)
            {
                throw new InvalidOperationException($"Failed to retrieve {licenseUrl}");
            }
            licenseResponse.EnsureSuccessStatusCode();
            var licenseHtml = await licenseResponse.Content.ReadAsStringAsync(cancellationToken);
                
            newDoc.LoadHtml(licenseHtml);
                
            return newDoc;
        }
            
        newDoc.LoadHtml(string.Empty);
            
        return newDoc;
    }
        
    public static async Task<HtmlDocument> TraverseToLicenseAsync(string url, IHttpClient httpClient, CancellationToken cancellationToken)
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
}