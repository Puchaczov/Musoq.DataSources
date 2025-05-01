using System.Collections.Generic;

namespace Musoq.DataSources.Roslyn.Components.NuGet.Http;

internal class HttpResponseMessageCacheItem
{
    public required Url Url { get; init; }
        
    public required byte[]? Content { get; init; }
        
    public required System.Net.HttpStatusCode StatusCode { get; init; }
        
    public required Dictionary<string, IEnumerable<string>> Headers { get; init; } = new();
}