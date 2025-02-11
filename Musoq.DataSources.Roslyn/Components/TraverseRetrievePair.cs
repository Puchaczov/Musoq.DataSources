using System;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace Musoq.DataSources.Roslyn.Components;

internal sealed record TraverseRetrievePair(Func<string, CancellationToken, Task<HtmlDocument>> TraverseAsync, Func<HtmlDocument, string?> Retrieve);