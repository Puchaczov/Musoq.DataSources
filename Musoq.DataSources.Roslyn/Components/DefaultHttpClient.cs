using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Musoq.DataSources.Roslyn.Components;

internal sealed class DefaultHttpClient(Func<HttpClient> createHttpClient) : IHttpClient
{
    private readonly HttpClient _httpClient = createHttpClient();

    public IHttpClient? NewInstance()
    {
        return new DefaultHttpClient(createHttpClient);
    }

    public IHttpClient? NewInstance(Action<HttpClient> configure)
    {
        var httpClient = createHttpClient();
        
        configure(httpClient);
        
        return new DefaultHttpClient(() => httpClient);
    }

    public async Task<HttpResponseMessage?> GetAsync(string requestUrl, CancellationToken cancellationToken)
    {
        return await _httpClient.GetAsync(requestUrl, cancellationToken);
    }

    public Task<HttpResponseMessage?> GetAsync(string requestUrl, Action<HttpClient> configure, CancellationToken cancellationToken)
    {
        configure(_httpClient);
        
        return GetAsync(requestUrl, cancellationToken);
    }

    public async Task<TOut?> PostAsync<T, TOut>(string requestUrl, T obj, CancellationToken cancellationToken) 
        where T : class
        where TOut : class
    {
        var content = new StringContent(JsonSerializer.Serialize(obj));
        var response = await _httpClient.PostAsync(requestUrl, content, cancellationToken);
        var result = await response.Content.ReadAsStringAsync(cancellationToken);
        
        if (string.IsNullOrEmpty(result))
            return null;
        
        return JsonSerializer.Deserialize<TOut>(result);
    }

    public async Task<TOut?> PostAsync<TOut>(string requestUrl, MultipartFormDataContent multipartFormDataContent,
        CancellationToken cancellationToken) where TOut : class
    {
        var response = await _httpClient.PostAsync(requestUrl, multipartFormDataContent, cancellationToken);
        var result = await response.Content.ReadAsStringAsync(cancellationToken);
        
        if (string.IsNullOrEmpty(result))
            return null;
        
        return JsonSerializer.Deserialize<TOut>(result);
    }

    public async Task<TOut?> PostAsync<TOut>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await _httpClient.SendAsync(request, cancellationToken);
        var result = await response.Content.ReadAsStringAsync(cancellationToken);
        
        if (string.IsNullOrEmpty(result))
            return default;
        
        return JsonSerializer.Deserialize<TOut>(result);
    }
}