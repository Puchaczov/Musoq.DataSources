using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Musoq.DataSources.Roslyn.Components;

internal interface IHttpClient
{
    Task<HttpResponseMessage?> GetAsync(string requestUrl, CancellationToken cancellationToken);

    Task<TOut?> PostAsync<T, TOut>(string requestUrl, T obj, CancellationToken cancellationToken) 
        where T : class 
        where TOut : class;
    
    Task<TOut?> PostAsync<TOut>(string requestUrl, MultipartFormDataContent multipartFormDataContent, CancellationToken cancellationToken) 
        where TOut : class;
}