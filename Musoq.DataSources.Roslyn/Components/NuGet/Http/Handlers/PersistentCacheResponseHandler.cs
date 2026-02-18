using System;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Musoq.DataSources.Roslyn.Components.NuGet.Helpers;

namespace Musoq.DataSources.Roslyn.Components.NuGet.Http.Handlers;

internal class PersistentCacheResponseHandler : DelegatingHandler
{
    private readonly AlwaysUpdateDirectoryView<Url, HttpResponseMessageCacheItem> _monitoredDirectory;
    private readonly SingleOperationCache<Url, HttpResponseMessageCacheItem> _responseCache;


    public PersistentCacheResponseHandler(string cacheDirectory, HttpMessageHandler handler, ILogger logger)
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
        _responseCache = new SingleOperationCache<Url, HttpResponseMessageCacheItem>();
        InnerHandler = handler;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var requestUri = request.RequestUri;

        if (requestUri is null)
            throw new InvalidOperationException("Request URL is null");

        var url = new Url(requestUri.ToString());
        var cacheUrl = url;
        var appendToPersistentCacheKeyHeader = request.Headers
            .FirstOrDefault(h =>
                h.Key.Equals("Musoq-Append-Url-Part-To-Persistent-Cache-Key", StringComparison.OrdinalIgnoreCase));

        if (appendToPersistentCacheKeyHeader.Key is not null && appendToPersistentCacheKeyHeader.Value.Any())
            cacheUrl = new Url(requestUri + "/" + appendToPersistentCacheKeyHeader.Value.First());

        if (_monitoredDirectory.TryGetValue(cacheUrl, out var responseMessage) && responseMessage is not null)
            return await responseMessage.FromCacheItemAsync();

        var cacheItem = await _responseCache.GetOrAddAsync(
            cacheUrl,
            async () =>
            {
                var response = await base.SendAsync(request, cancellationToken);

                var cacheFiledResponseHeader = request.Headers
                    .FirstOrDefault(h =>
                        h.Key.Equals("Musoq-Cache-Failed-Response", StringComparison.OrdinalIgnoreCase));

                var cacheFailedResponseString = cacheFiledResponseHeader.Value?.FirstOrDefault() ?? "false";
                var cacheFailedResponse = bool.TryParse(cacheFailedResponseString, out var result) && result;

                if (!cacheFailedResponse) response.EnsureSuccessStatusCode();

                response.RequestMessage ??= request;

                if (!response.IsSuccessStatusCode && !cacheFailedResponse)
                    return await response.ToCacheItemAsync();

                var cacheItem = await response.ToCacheItemAsync();

                _monitoredDirectory.Add(cacheUrl, cacheItem);

                return cacheItem;
            }, cancellationToken: cancellationToken);

        return await cacheItem.FromCacheItemAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _monitoredDirectory.Dispose();

        base.Dispose(disposing);
    }

    private static HttpResponseMessageCacheItem GetDestinationValue(string filePath, IFileSystem fileSystem,
        CancellationToken cancellationToken)
    {
        var content = fileSystem.ReadAllText(filePath, cancellationToken);
        var item = JsonSerializer.Deserialize<HttpResponseMessageCacheItem>(content);

        if (item == null) throw new InvalidOperationException($"Failed to deserialize cached item from {filePath}");

        return item;
    }

    private static string ConvertKeyToPath(Url arg)
    {
        var fileName = UrlToFileName(arg);
        return $"{fileName}.json";
    }

    private static void UpdateDirectory(string filePath, HttpResponseMessageCacheItem responseMessage,
        IFileSystem fileSystem, CancellationToken token)
    {
        if (fileSystem.IsFileExists(filePath))
            return;

        var fileContent = JsonSerializer.Serialize(responseMessage);
        fileSystem.WriteAllText(filePath, fileContent, token);
    }

    private static string UrlToFileName(Url url)
    {
        var urlHash = MD5.HashData(
            Encoding.UTF8.GetBytes(url.Value));

        return string.Concat(
                urlHash.Select(b => b.ToString("x2"))
            ).Replace(":", "_")
            .Replace("/", "_")
            .Replace("\\", "_")
            .Replace("?", "_")
            .Replace("=", "_")
            .Replace("&", "_");
    }
}