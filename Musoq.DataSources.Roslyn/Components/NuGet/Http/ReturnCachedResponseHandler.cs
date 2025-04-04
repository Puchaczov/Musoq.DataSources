using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Musoq.DataSources.Roslyn.Components.NuGet.Http;

internal class ReturnCachedResponseHandler : DelegatingHandler
{
    private readonly ConcurrentDictionary<string, (HttpResponseMessage? HttpResponseMessage, SemaphoreSlim Guard)> _cachedItems = new();
    private readonly IFileSystem _fileSystem;
    private readonly string _cacheDirectory;

    public ReturnCachedResponseHandler(IFileSystem fileSystem, string cacheDirectory, HttpMessageHandler handler)
    {
        _fileSystem = fileSystem;
        _cacheDirectory = cacheDirectory;
        
        InnerHandler = handler;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (!IFileSystem.DirectoryExists(_cacheDirectory))
        {
            IFileSystem.CreateDirectory(_cacheDirectory);
        }
        
        await foreach (var cachedItemPath in _fileSystem.GetFilesAsync(_cacheDirectory, false, cancellationToken))
        {
            if (IFileSystem.GetExtension(cachedItemPath) != ".json")
            {
                continue;
            }
            
            HttpResponseMessageCacheItem? cacheItem;
            try
            {
                var fileContent = await _fileSystem.ReadAllTextAsync(cachedItemPath, cancellationToken);
                cacheItem = System.Text.Json.JsonSerializer.Deserialize<HttpResponseMessageCacheItem>(fileContent);
            }
            catch (Exception)
            {
                continue;
            }

            if (cacheItem == null) continue;
            
            var responseMessage = new HttpResponseMessage(cacheItem.StatusCode)
            {
                Content = new ByteArrayContent(cacheItem.Content ?? [])
            };
                
            foreach (var header in cacheItem.Headers)
            {
                responseMessage.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
                
            _cachedItems.TryAdd(
                cacheItem.Url.Value, 
                (responseMessage, new SemaphoreSlim(1, 1))
            );
        }
    }

    public void Initialize()
    {
        Task.Run(async () => await InitializeAsync(CancellationToken.None)).Wait();
    }
    
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri?.ToString();
        
        if (url == null)
            throw new InvalidOperationException("Request URL is null");
        
        if (_cachedItems.TryGetValue(url, out var cachedItems))
            return cachedItems.HttpResponseMessage ?? throw new InvalidOperationException($"Cached response for {url} is null");
        
        var response = await base.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode) 
            return response;

        try
        {
            _cachedItems.TryAdd(url, (response, new SemaphoreSlim(1, 1)));

            if (!_cachedItems.TryGetValue(url, out cachedItems))
            {
                throw new InvalidOperationException($"Failed to retrieve cached response for {url}");
            }

            await cachedItems.Guard.WaitAsync(cancellationToken);

            var cacheItem = new HttpResponseMessageCacheItem
            {
                Url = new Url(url),
                Content = await response.Content.ReadAsByteArrayAsync(cancellationToken),
                StatusCode = response.StatusCode,
                Headers = response.Headers.ToDictionary(h => h.Key, h => h.Value)
            };

            var fileName = IFileSystem.Combine(_cacheDirectory, $"{UrlToFileName(cacheItem.Url)}.json");
            var fileContent = System.Text.Json.JsonSerializer.Serialize(cacheItem);
            await _fileSystem.WriteAllTextAsync(fileName, fileContent, cancellationToken);

            return response;
        }
        catch
        {
            if (response.IsSuccessStatusCode)
                return response;

            throw;
        }
        finally
        {
            cachedItems.Guard.Release();
        }
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

    private class HttpResponseMessageCacheItem
    {
        public required Url Url { get; init; }
        
        public required byte[]? Content { get; init; }
        
        public required System.Net.HttpStatusCode StatusCode { get; init; }
        
        public required Dictionary<string, IEnumerable<string>> Headers { get; init; } = new();
    }

    private record Url(string Value);
}