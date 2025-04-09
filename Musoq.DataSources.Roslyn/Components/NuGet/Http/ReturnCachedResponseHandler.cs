using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Musoq.DataSources.Roslyn.Components.NuGet.Http;

internal class ReturnCachedResponseHandler : DelegatingHandler
{
    private readonly AlwaysUpdateDirectoryView<Url, HttpResponseMessageCacheItem> _monitoredDirectory;

    public ReturnCachedResponseHandler(string cacheDirectory, HttpMessageHandler handler, ILogger logger)
    {
        _monitoredDirectory = new AlwaysUpdateDirectoryView<Url, HttpResponseMessageCacheItem>(
            cacheDirectory,
            GetDestinationValue, 
            ConvertKeyToPath, 
            UpdateDirectory,
            null,
            null,
            logger
        );
        InnerHandler = handler;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var requestUri = request.RequestUri;
        
        if (requestUri is null)
            throw new InvalidOperationException("Request URL is null");

        var url = new Url(requestUri.ToString());
        
        if (_monitoredDirectory.TryGetValue(url, out var responseMessage) && responseMessage is not null)
            return await FromCacheItemAsync(responseMessage);
        
        var response = await base.SendAsync(request, cancellationToken);

        var cacheFiledResponseHeader = request.Headers
            .FirstOrDefault(h => h.Key.Equals("Musoq-Cache-Failed-Response", StringComparison.OrdinalIgnoreCase));

        var cacheFailedResponseString = cacheFiledResponseHeader.Value?.FirstOrDefault() ?? "false";
        var cacheFailedResponse = bool.TryParse(cacheFailedResponseString, out var result) && result;
        
        if (!response.IsSuccessStatusCode && !cacheFailedResponse) 
            return response;

        var cacheItem = await ToCacheItemAsync(url, response);
        
        _monitoredDirectory.Add(url, cacheItem);
            
        return await FromCacheItemAsync(cacheItem);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _monitoredDirectory.Dispose();
        }
        
        base.Dispose(disposing);
    }

    private static HttpResponseMessageCacheItem GetDestinationValue(string filePath, IFileSystem fileSystem, CancellationToken cancellationToken)
    {
        var content = fileSystem.ReadAllText(filePath, cancellationToken);
        var item = System.Text.Json.JsonSerializer.Deserialize<HttpResponseMessageCacheItem>(content);
        
        if (item == null)
        {
            throw new InvalidOperationException($"Failed to deserialize cached item from {filePath}");
        }

        return item;
    }

    private static string ConvertKeyToPath(Url arg)
    {
        var fileName = UrlToFileName(arg);
        return $"{fileName}.json";
    }

    private static void UpdateDirectory(string filePath, HttpResponseMessageCacheItem responseMessage, IFileSystem fileSystem, CancellationToken token)
    {
        if (fileSystem.IsFileExists(filePath))
            return;

        var fileContent = System.Text.Json.JsonSerializer.Serialize(responseMessage);
        fileSystem.WriteAllText(filePath, fileContent, token);
    }

    private static string UrlToFileName(Url url)
    {
        var urlHash = System.Security.Cryptography.MD5.HashData(
            System.Text.Encoding.UTF8.GetBytes(url.Value));
        
        return string.Concat(
                urlHash.Select(b => b.ToString("x2"))
        ).Replace(":", "_")
            .Replace("/", "_")
            .Replace("\\", "_");
    }

    private static async Task<HttpResponseMessageCacheItem> ToCacheItemAsync(Url url, HttpResponseMessage responseMessage)
    {
        return new HttpResponseMessageCacheItem
        {
            Url = url,
            Content = await responseMessage.Content.ReadAsByteArrayAsync(),
            StatusCode = responseMessage.StatusCode,
            Headers = responseMessage.Headers.ToDictionary(h => h.Key, h => h.Value)
        };
    }
    
    private static async Task<HttpResponseMessage> FromCacheItemAsync(HttpResponseMessageCacheItem item)
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

    private class HttpResponseMessageCacheItem
    {
        public required Url Url { get; init; }
        
        public required byte[]? Content { get; init; }
        
        public required System.Net.HttpStatusCode StatusCode { get; init; }
        
        public required Dictionary<string, IEnumerable<string>> Headers { get; init; } = new();
    }

    private record Url(string Value);
}