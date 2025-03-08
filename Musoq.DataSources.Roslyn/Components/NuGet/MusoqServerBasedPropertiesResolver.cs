using System.Threading;
using System.Threading.Tasks;

namespace Musoq.DataSources.Roslyn.Components.NuGet;

internal class MusoqServerBasedPropertiesResolver : IAiBasedPropertiesResolver
{
    public Task<string[]> GetLicenseNamesAsync(string licenseContent, CancellationToken cancellationToken)
    {
        throw new System.NotImplementedException();
    }

    public Task<string[]> GetLicenseNamesByLicenseUrlAsync(string licenseUrl, CancellationToken cancellationToken)
    {
        throw new System.NotImplementedException();
    }

    public Task<string[]> GetLicenseNamesBySourceRepositoryUrlAsync(string sourceRepositoryUrl, CancellationToken cancellationToken)
    {
        throw new System.NotImplementedException();
    }

    public Task<string?> GetLicenseUrlBySourceRepositoryUrlAsync(string sourceRepositoryUrl, CancellationToken cancellationToken)
    {
        throw new System.NotImplementedException();
    }
}