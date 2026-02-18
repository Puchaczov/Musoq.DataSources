using System.Collections.Generic;
using System.Net;

namespace Musoq.DataSources.Roslyn.Components.NuGet.Http;

internal class HttpResponseMessageCacheItem
{
    public required Url Url { get; init; }

    public required byte[]? Content { get; init; }

    public required HttpStatusCode StatusCode { get; init; }

    public required Dictionary<string, IEnumerable<string>> Headers { get; init; } = new();
}