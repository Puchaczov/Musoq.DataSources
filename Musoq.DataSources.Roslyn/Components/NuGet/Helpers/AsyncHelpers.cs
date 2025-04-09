using System.Collections.Generic;
using System.Threading.Tasks;

namespace Musoq.DataSources.Roslyn.Components.NuGet.Helpers;

internal static class AsyncHelpers
{
    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            await Task.Yield();
            yield return item;
        }
    }
}