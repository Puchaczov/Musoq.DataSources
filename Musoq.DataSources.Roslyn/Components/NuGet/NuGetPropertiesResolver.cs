using System.Threading;
using System.Threading.Tasks;

namespace Musoq.DataSources.Roslyn.Components.NuGet;

internal class NuGetPropertiesResolver(string baseUrl, IHttpClient httpClient) : INuGetPropertiesResolver
{
    public async Task<string[]> GetLicenseNamesAsync(string licenseContent, CancellationToken cancellationToken)
    {
        var licenses = await httpClient.PostAsync<string, string[]>($"{baseUrl}/model/licensesextractor_0.1/license", licenseContent, cancellationToken);
        
        return licenses ?? [];
    }
}