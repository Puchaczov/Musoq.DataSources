using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace Musoq.DataSources.Roslyn.Components.NuGet.Helpers;

internal static class NugetHelpers
{
    public static async Task<Func<Task<string?>>> DiscoverLicensesNamesAsync(string url, NuGetResource commonResources, IHttpClient httpClient, INuGetPropertiesResolver nuGetPropertiesResolver, CancellationToken cancellationToken)
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
            
            htmlDocument = newDoc;
            
            commonResources.AddHtmlDocument(url, htmlDocument);
        }

        if (htmlDocument is null)
        {
            return () => Task.FromResult<string?>(null);
        }
        
        //License info is available on the page
        if (TryExtractUrl(htmlDocument, "//a[@data-track='outbound-license-url']", "href", out var licenseUrl) && licenseUrl is not null)
        {
            if (licenseUrl.StartsWith('/'))
            {
                var nugetOrgLicenseUrl = CombineUrl("https://www.nuget.org", licenseUrl);
                var licenseContentPageHttpResponseMessage = await httpClient.GetAsync(nugetOrgLicenseUrl, cancellationToken);
                var pageContent = licenseContentPageHttpResponseMessage is not null ? await licenseContentPageHttpResponseMessage.Content.ReadAsStringAsync(cancellationToken) : null;
                var licenseContentHtmlDocument = new HtmlDocument();
                licenseContentHtmlDocument.LoadHtml(pageContent);

                if (TryExtractContent(licenseContentHtmlDocument, "//pre[@class='license-file-contents custom-license-container']", out var licenseContentFromPage) && licenseContentFromPage is not null)
                {
                    return async () => System.Text.Json.JsonSerializer.Serialize(await nuGetPropertiesResolver.GetLicensesNamesAsync(licenseContentFromPage, cancellationToken));
                }

                if (TryExtractContent(licenseContentHtmlDocument, "//div[@class='common-licenses']",
                        out licenseContentFromPage) && licenseContentFromPage is not null)
                {
                    return async () => System.Text.Json.JsonSerializer.Serialize(await nuGetPropertiesResolver.GetLicensesNamesAsync(licenseContentFromPage, cancellationToken));
                }
                
                return () => Task.FromResult<string?>(null);
            }
            
            var licenseContentHttpResponseMessage = await httpClient.GetAsync(ConvertToRawFileUrl(licenseUrl, null), cancellationToken);
            var licenseContent = licenseContentHttpResponseMessage is not null ? await licenseContentHttpResponseMessage.Content.ReadAsStringAsync(cancellationToken) : null;
            
            if (licenseContent is null)
            {
                return () => Task.FromResult<string?>(null);
            }
            
            return async () => System.Text.Json.JsonSerializer.Serialize(await nuGetPropertiesResolver.GetLicensesNamesAsync(licenseContent, cancellationToken));
        }
        
        //Source repository is available on the page
        if (TryExtractUrl(htmlDocument, "//a[@data-track='outbound-repository-url']", "href", out var sourceRepositoryUrl) && sourceRepositoryUrl is not null)
        {
            List<string> licenseUrls = [];
            
            await foreach(var licenseUrlLicenseContentPair in FindLicenseAsync(sourceRepositoryUrl, httpClient, (_, _) => true, cancellationToken))
            {
                foreach (var licenseName in await nuGetPropertiesResolver.GetLicensesNamesAsync(licenseUrlLicenseContentPair.LicenseContent, cancellationToken))
                {
                    licenseUrls.Add(licenseName);
                }
            }
            
            return () => Task.FromResult<string?>(System.Text.Json.JsonSerializer.Serialize(licenseUrls));
        }
            
        return () => Task.FromResult<string?>(null);
    }
    
    public static async Task<Func<Task<string?>>> DiscoverLicenseUrlAsync(string url, NuGetResource commonResources, IHttpClient httpClient, INuGetPropertiesResolver nuGetPropertiesResolver, CancellationToken cancellationToken)
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
            
            htmlDocument = newDoc;
            
            commonResources.AddHtmlDocument(url, htmlDocument);
        }
        
        if (htmlDocument is null)
        {
            return () => Task.FromResult<string?>(null);
        }
        
        if (TryExtractUrl(htmlDocument, "//a[@data-track='outbound-license-url']", "href", out var licenseUrl) && licenseUrl is not null)
        {
            return () => Task.FromResult<string?>(licenseUrl);
        }
        
        if (TryExtractUrl(htmlDocument, "//a[@data-track='outbound-repository-url']", "href", out var sourceRepositoryUrl) && sourceRepositoryUrl is not null)
        {
            var lookFor = commonResources.LookingForLicense;
                
            if (lookFor is null)
            {
                return () => Task.FromResult<string?>(null);
            }
            
            var firstLicense = await FindFirstLicenseUrlAsync(
                sourceRepositoryUrl, 
                httpClient, 
                (fileName, fileContent) => fileName.Contains(lookFor, StringComparison.OrdinalIgnoreCase) || fileContent.Contains(lookFor, StringComparison.OrdinalIgnoreCase),
                cancellationToken);
            return () => Task.FromResult(firstLicense);
        }
        
        return () => Task.FromResult<string?>(null);
    }
    
    public static async Task<Func<Task<string?>>> DiscoverLicenseContentAsync(string url, NuGetResource commonResources, IHttpClient httpClient, INuGetPropertiesResolver aiBasedPropertiesResolver, CancellationToken cancellationToken)
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
            
            htmlDocument = newDoc;
            
            commonResources.AddHtmlDocument(url, htmlDocument);
        }
        
        if (htmlDocument is null)
        {
            return () => Task.FromResult<string?>(null);
        }
        
        if (TryExtractUrl(htmlDocument, "//a[@data-track='outbound-license-url']", "href", out var licenseUrl) && licenseUrl is not null)
        {
            return async () =>
            {
                var convertedUrl = ConvertToRawFileUrl(licenseUrl, null);
                var httpResponseMessage = await httpClient.GetAsync(convertedUrl, cancellationToken);
                var httpResponseMessageContent = httpResponseMessage is not null ? await httpResponseMessage.Content.ReadAsStringAsync(cancellationToken) : null;
                var document = new HtmlDocument();
                document.LoadHtml(httpResponseMessageContent);
                
                if (TryExtractContent(document, "//pre[@class='license-file-contents custom-license-container']", out var licenseContent) && licenseContent is not null)
                {
                    return licenseContent;
                }
                
                return httpResponseMessage is not null ? await httpResponseMessage.Content.ReadAsStringAsync(cancellationToken) : null;
            };
        }
        
        if (TryExtractUrl(htmlDocument, "//a[@data-track='outbound-repository-url']", "href", out var sourceRepositoryUrl) && sourceRepositoryUrl is not null)
        {
            return async () =>
            {
                var lookFor = commonResources.LookingForLicense;
                
                if (lookFor is null)
                {
                    return null;
                }
                
                var firstLicense = await FindFirstLicenseUrlAsync(
                    sourceRepositoryUrl, 
                    httpClient, 
                    (fileName, fileContent) => fileName.Contains(lookFor, StringComparison.OrdinalIgnoreCase) || fileContent.Contains(lookFor, StringComparison.OrdinalIgnoreCase), 
                    cancellationToken
                );
                
                if (firstLicense == null)
                {
                    return null;
                }
                
                var httpResponseMessage = await httpClient.GetAsync(ConvertToRawFileUrl(firstLicense, null), cancellationToken);
                
                return httpResponseMessage is not null ? await httpResponseMessage.Content.ReadAsStringAsync(cancellationToken) : null;
            };
        }
        
        return () => Task.FromResult<string?>(null);
    }

    private static async Task<string?> FindFirstLicenseUrlAsync(string sourceRepositoryUrl, IHttpClient httpClient, Func<string, string, bool> filterBasedOnFileNameAndContent, CancellationToken cancellationToken)
    {
        await foreach (var licenseUrlLicenseContentPair in FindLicenseAsync(sourceRepositoryUrl, httpClient, filterBasedOnFileNameAndContent, cancellationToken))
        {
            return licenseUrlLicenseContentPair.LicenseUrl;
        }
        
        return null;
    }

    private static async IAsyncEnumerable<(string LicenseUrl, string LicenseContent)> FindLicenseAsync(string sourceRepositoryUrl, IHttpClient httpClient, Func<string, string, bool> filter, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        sourceRepositoryUrl = sourceRepositoryUrl.TrimEnd('/');
        
        var licenseFileNames = new[]
        {
            "LICENSE",
            "LICENSE.md",
            "LICENSE.txt",
            "License.txt",
            "COPYING",
            "COPYING.md",
            "COPYING.txt",
            "LICENSE.MIT",
            "LICENSE.APACHE",
            "LICENSE.BSD"
        };

        if (!sourceRepositoryUrl.Contains("github.com") && !sourceRepositoryUrl.Contains("gitlab.com")) yield break;
        
        sourceRepositoryUrl = sourceRepositoryUrl
            .TrimEnd('/')
            .Replace(".git", string.Empty);

        string branch;
                
        if (sourceRepositoryUrl.Contains("github.com"))
        {
            branch = await GetDefaultGithubBranchAsync(sourceRepositoryUrl, httpClient, cancellationToken);
                
            sourceRepositoryUrl += $"/blob/{branch}";
        }
        else if (sourceRepositoryUrl.Contains("gitlab.com"))
        {
            branch = await GetDefaultGitlabBranchAsync(sourceRepositoryUrl, httpClient, cancellationToken);
                
            sourceRepositoryUrl += $"/-/blob/{branch}";
        }
        else
        {
            throw new NotSupportedException($"Unsupported repository URL: {sourceRepositoryUrl}");
        }

        httpClient = httpClient.NewInstance(client =>
        {
            client.DefaultRequestHeaders.Add("Musoq-Cache-Failed-Response", "true");
        });
            
        foreach (var fileName in licenseFileNames)
        {
            var licenseUrl = sourceRepositoryUrl + "/" + fileName;
            var response = await httpClient.GetAsync(ConvertToRawFileUrl(licenseUrl, branch), cancellationToken);
                
            if (response is not { IsSuccessStatusCode: true })
            {
                continue;
            }

            var licenseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                
            if (filter(fileName, licenseContent))
            {
                yield return (licenseUrl, licenseContent);
            }
        }
    }

    private static async Task<string> GetDefaultGithubBranchAsync(string sourceRepositoryUrl, IHttpClient httpClient, CancellationToken cancellationToken)
    {
        httpClient = httpClient.NewInstance();
        
        sourceRepositoryUrl = sourceRepositoryUrl.TrimEnd('/');

        var parts = sourceRepositoryUrl.Split('/');
        var owner = parts[3];
        var repo = parts[4];
        
        var url = $"https://api.github.com/repos/{owner}/{repo}";
        
        var response = await httpClient.GetAsync(url, configure =>
        {
            configure.DefaultRequestHeaders.UserAgent.TryParseAdd("request");
        }, cancellationToken);
        
        if (response is null)
        {
            throw new InvalidOperationException($"Failed to retrieve {url}");
        }
        
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var jsonDocument = System.Text.Json.JsonDocument.Parse(json);
        
        var defaultBranch = jsonDocument.RootElement.GetProperty("default_branch").GetString();
        
        if (string.IsNullOrEmpty(defaultBranch))
        {
            throw new InvalidOperationException($"Failed to retrieve default branch for {sourceRepositoryUrl}");
        }
        
        return defaultBranch;
    }
    
    private static async Task<string> GetDefaultGitlabBranchAsync(string sourceRepositoryUrl, IHttpClient httpClient, CancellationToken cancellationToken)
    {
        httpClient = httpClient.NewInstance();
        
        sourceRepositoryUrl = sourceRepositoryUrl.TrimEnd('/');

        var parts = sourceRepositoryUrl.Split('/');
        var owner = parts[3];
        var repo = parts[4];
        
        var url = $"https://gitlab.com/api/v4/projects/{owner}%2F{repo}";
        
        var response = await httpClient.GetAsync(url, configure =>
        {
            configure.DefaultRequestHeaders.UserAgent.TryParseAdd("request");
        }, cancellationToken);
        
        if (response is null)
        {
            throw new InvalidOperationException($"Failed to retrieve {url}");
        }
        
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var jsonDocument = System.Text.Json.JsonDocument.Parse(json);
        
        var defaultBranch = jsonDocument.RootElement.GetProperty("default_branch").GetString();
        
        if (string.IsNullOrEmpty(defaultBranch))
        {
            throw new InvalidOperationException($"Failed to retrieve default branch for {sourceRepositoryUrl}");
        }
        
        return defaultBranch;
    }

    private static string ConvertToRawFileUrl(string repositoryUrl, string? branch)
    {
        repositoryUrl = repositoryUrl.TrimEnd('/');
        
        if (repositoryUrl.Contains("github.com"))
        {
            var parts = repositoryUrl.Split(["github.com/"], StringSplitOptions.None)[1];
            var segments = parts.Split('/');
            
            var owner = segments[0];
            var repo = segments[1];
            
            if (segments.Length > 3 && segments[2] == "blob")
            {
                branch = segments[3];
            }
            
            var file = segments[4];
            
            return $"https://raw.githubusercontent.com/{owner}/{repo}/refs/heads/{branch}/{file}";
        }

        if (repositoryUrl.Contains("gitlab.com"))
        {
            if (repositoryUrl.Contains("/-/raw/"))
                return repositoryUrl + "/";
                
            var parts = repositoryUrl.Split(["gitlab.com/"], StringSplitOptions.None)[1];
            var segments = parts.Split('/');
            
            if (segments.Length > 3 && segments[2] == "-" && segments[3] == "blob")
            {
                branch = segments[4];
            }
            
            var file = segments[^1];
            
            return $"{repositoryUrl}/-/raw/{branch}/{file}";
        }
        
        if (repositoryUrl.StartsWith("/"))
        {
            const string nugetOrg = "https://nuget.org";
            
            if (repositoryUrl.StartsWith("/"))
            {
                return CombineUrl(nugetOrg, repositoryUrl);
            }

            return CombineUrl(nugetOrg, repositoryUrl);
        }

        return repositoryUrl;
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
    
    private static bool TryExtractContent(HtmlDocument doc, string xpath, out string? content)
    {
        var node = doc.DocumentNode.SelectSingleNode(xpath);
        
        if (node is null)
        {
            content = null;
            return false;
        }
        
        content = node.InnerText;
        return true;
    }

    private static string CombineUrl(string baseUrl, string path)
    {
        baseUrl = baseUrl.TrimEnd('/');
        path = path.StartsWith('/') ? path : "/" + path;
        
        return baseUrl + path;
    }
}