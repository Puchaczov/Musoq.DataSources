using System.Collections;
using System.Collections.Concurrent;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.AsyncRowsSource;

public class ChunkedSource(
    BlockingCollection<IReadOnlyList<IObjectResolver>> readRows,
    CancellationToken token,
    Func<Exception?> getParentException)
    : IEnumerable<IObjectResolver>
{
    public IEnumerator<IObjectResolver> GetEnumerator()
    {
        return new ChunkEnumerator(readRows, getParentException, token);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}