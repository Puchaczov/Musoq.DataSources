using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Musoq.DataSources.Roslyn.Components;

internal sealed class DefaultHttpClient : IHttpClient
{
    private static readonly HttpClient HttpClient = new();

    public async Task<HttpResponseMessage?> GetAsync(string requestUrl, CancellationToken cancellationToken)
    {
        return await HttpClient.GetAsync(requestUrl, cancellationToken);
    }

    public async Task<TOut?> PostAsync<T, TOut>(string requestUrl, T obj, CancellationToken cancellationToken) 
        where T : class
        where TOut : class
    {
        var content = new StringContent(JsonSerializer.Serialize(obj));
        var response = await HttpClient.PostAsync(requestUrl, content, cancellationToken);
        var result = await response.Content.ReadAsStringAsync(cancellationToken);
        
        if (string.IsNullOrEmpty(result))
            return null;
        
        return JsonSerializer.Deserialize<TOut>(result);
    }

    public async Task<TOut?> PostAsync<TOut>(string requestUrl, MultipartFormDataContent multipartFormDataContent,
        CancellationToken cancellationToken) where TOut : class
    {
        var response = await HttpClient.PostAsync(requestUrl, multipartFormDataContent, cancellationToken);
        var result = await response.Content.ReadAsStringAsync(cancellationToken);
        
        if (string.IsNullOrEmpty(result))
            return null;
        
        return JsonSerializer.Deserialize<TOut>(result);
    }
}