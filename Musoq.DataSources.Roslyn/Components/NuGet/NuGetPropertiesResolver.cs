using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Musoq.DataSources.Roslyn.Components.NuGet;

/// <summary>
/// This class is responsible for resolving NuGet properties.
/// </summary>
/// <param name="baseUrl">Base URL for the NuGet properties resolver.</param>
/// <param name="httpClient">HTTP client for making requests.</param>
public class NuGetPropertiesResolver(string baseUrl, IHttpClient? httpClient) : INuGetPropertiesResolver
{
    private readonly ConcurrentDictionary<string, LicensesResult> _cachedLicenseContentResponses = new();
    
    /// <summary>
    /// Asynchronously retrieves the names of licenses from the given license content.
    /// </summary>
    /// <param name="licenseContent">The content of the license.</param>
    /// <param name="cancellationToken">Cancellation token for cancellation.</param>
    /// <returns>An array of license names.</returns>
    public async Task<string[]> GetLicensesNamesAsync(string licenseContent, CancellationToken cancellationToken)
    {
        if (_cachedLicenseContentResponses.TryGetValue(licenseContent, out var cachedResponse))
            return cachedResponse.Response.Select(f => f.LicenseName).ToArray();
        
        using var formData = new MultipartFormDataContent();
        
        const string jsonSchema = 
            """{"type": "array","items": [{"type": "object","properties": {"licenseName": {"type": "string"}},"required": ["licenseName"]}]}""";

        var chat = new
        {
            messages = new[]
            {
                new { 
                    content = new
                    {
                        value = licenseContent,
                        type = 0
                    }, 
                    role = "user"
                }
            }
        };
        using var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(chat)));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/text");
        
        using var stringContent = new StringContent(jsonSchema);
        
        formData.Add(stringContent, "ResponseFormat");
        formData.Add(fileContent, "file", "chat.json");
        
        var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/model/what-licenses-are-here");
        httpRequestMessage.Headers.Add("Musoq-Append-Url-Part-To-Persistent-Cache-Key", ComputeLicenseContentMd5(licenseContent));
        httpRequestMessage.Content = formData;
        
        var response = await httpClient.PostAsync<LicensesResult>(httpRequestMessage, cancellationToken);
        
        if (response is not null)
            _cachedLicenseContentResponses.TryAdd(licenseContent, response);
        
        return response is null ? [] : response.Response.Select(f => f.LicenseName).ToArray();
    }
    
    private static string ComputeLicenseContentMd5(string licenseContent)
    {
        var hash = System.Security.Cryptography.MD5.HashData(Encoding.UTF8.GetBytes(licenseContent));
        return Convert.ToBase64String(hash)
            .Replace("=", "~")
            .Replace("+", "-")
            .Replace("/", "_");
    }

    private class LicensesResult
    {
        [JsonPropertyName("response")]
        public required Licenses[] Response { get; init; }
    }
    
    private class Licenses
    {
        [JsonPropertyName("licenseName")]
        public required string LicenseName { get; init; }
    }
}