using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Musoq.DataSources.Roslyn.Components
{
    internal interface IHttpClient
    {
        Task<HttpResponseMessage?> GetAsync(string requestUrl, CancellationToken cancellationToken);
    }
}
