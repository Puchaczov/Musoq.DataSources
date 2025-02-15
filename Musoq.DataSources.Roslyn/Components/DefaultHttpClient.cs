using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Musoq.DataSources.Roslyn.Components
{
    internal sealed class DefaultHttpClient : IHttpClient
    {
        private static readonly HttpClient _httpClient = new();

        public async Task<HttpResponseMessage?> GetAsync(string requestUrl, CancellationToken cancellationToken)
        {
            return await _httpClient.GetAsync(requestUrl, cancellationToken);
        }
    }
}
