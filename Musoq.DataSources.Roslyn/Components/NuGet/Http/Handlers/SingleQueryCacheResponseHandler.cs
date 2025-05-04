using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Musoq.DataSources.Roslyn.Components.NuGet.Helpers;

namespace Musoq.DataSources.Roslyn.Components.NuGet.Http.Handlers;

internal class SingleQueryCacheResponseHandler(HttpMessageHandler innerHandler) : DelegatingHandler(innerHandler)
{
    private readonly SingleOperationCache<Url, HttpResponseMessageCacheItem> _responseCache = new();

    public SingleQueryCacheResponseHandler()
        : this(new HttpClientHandler())
    {
    }
    
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var requestUri = request.RequestUri;
        
        if (requestUri is null)
            throw new InvalidOperationException("Request URL is null");

        var url = new Url(requestUri.ToString());
        
        var cacheItem = await _responseCache.GetOrAddAsync(
            url,
            async () => 
            {
                var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
                return await response.ToCacheItemAsync();
            },
            _ => true,
            cancellationToken);
        
        return await cacheItem.FromCacheItemAsync();
    }
    
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _responseCache.Dispose();
        }
        
        base.Dispose(disposing);
    }
}