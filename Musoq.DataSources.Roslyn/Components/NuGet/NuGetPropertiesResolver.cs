using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Musoq.DataSources.Roslyn.Components.NuGet;

internal class NuGetPropertiesResolver(string baseUrl, IHttpClient httpClient) : INuGetPropertiesResolver
{
    public async Task<string[]> GetLicenseNamesAsync(string licenseContent, CancellationToken cancellationToken)
    {
        using var formData = new MultipartFormDataContent();
        
        const string jsonSchema = 
            """{"$schema": "http://json-schema.org/draft-04/schema#","type": "array","items": [{"type": "object","properties": {"licenseName": {"type": "string"}},"required": ["licenseName"]},{"type": "object","properties": {"licenseName": {"type":"string"}},"required": ["licenseName"]}]}""";
        
        using var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(licenseContent));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/text");
        
        formData.Add(new StringContent(jsonSchema), "ResponseFormat");
        formData.Add(fileContent, "file", "chat.json");
        
        var response = await httpClient.PostAsync<Licenses[]>($"{baseUrl}/model/what-licenses-are-here", formData, cancellationToken);
        
        return response is null ? [] : response.Select(f => f.LicenseName).ToArray();
    }
    
    private class Licenses
    {
        [JsonPropertyName("licenseName")]
        public required string LicenseName { get; init; }
    }
}