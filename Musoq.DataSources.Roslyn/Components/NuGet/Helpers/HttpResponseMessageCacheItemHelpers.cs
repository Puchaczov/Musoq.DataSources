using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Musoq.DataSources.Roslyn.Components.NuGet.Http;

namespace Musoq.DataSources.Roslyn.Components.NuGet.Helpers;

internal static class HttpResponseMessageCacheItemHelpers
{
    public static async Task<HttpResponseMessageCacheItem> ToCacheItemAsync(this HttpResponseMessage responseMessage)
    {
        return new HttpResponseMessageCacheItem
        {
            Url = new Url(responseMessage.RequestMessage?.RequestUri?.ToString() ?? throw new InvalidOperationException("Request URI is null")),
            Content = await responseMessage.Content.ReadAsByteArrayAsync(),
            StatusCode = responseMessage.StatusCode,
            Headers = responseMessage.Headers.ToDictionary(h => h.Key, h => h.Value)
        };
    }
    
    public static async Task<HttpResponseMessage> FromCacheItemAsync(this HttpResponseMessageCacheItem item)
    {
        var response = new HttpResponseMessage(item.StatusCode)
        {
            Content = new ByteArrayContent(item.Content ?? [])
        };

        foreach (var header in item.Headers)
        {
            response.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return await Task.FromResult(response);
    }
}