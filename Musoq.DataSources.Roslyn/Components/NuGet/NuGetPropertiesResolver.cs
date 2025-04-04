using System.Collections.Concurrent;
using System.Diagnostics;
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
public class NuGetPropertiesResolver(string baseUrl, IHttpClient httpClient) : INuGetPropertiesResolver
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
            """{"$schema": "http://json-schema.org/draft-04/schema#","type": "array","items": [{"type": "object","properties": {"licenseName": {"type": "string"}},"required": ["licenseName"]},{"type": "object","properties": {"licenseName": {"type":"string"}},"required": ["licenseName"]}]}""";

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
        
        formData.Add(new StringContent(jsonSchema), "ResponseFormat");
        formData.Add(fileContent, "file", "chat.json");
        
        var response = await httpClient.PostAsync<LicensesResult>($"{baseUrl}/model/what-licenses-are-here", formData, cancellationToken);
        
        if (response is not null)
            _cachedLicenseContentResponses.TryAdd(licenseContent, response);
        
        if (response is null)
            Debugger.Break();
        
        return response is null ? [] : response.Response.Select(f => f.LicenseName).ToArray();
    }
    
    private class LicensesResult
    {
        [JsonPropertyName("response")]
        public Licenses[] Response { get; init; }
    }
    
    private class Licenses
    {
        [JsonPropertyName("licenseName")]
        public required string LicenseName { get; init; }
    }
}