using System.Collections;
using System.Collections.Concurrent;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.InferrableDataSourceHelpers;

public class ChunkedSource : IEnumerable<IObjectResolver>
{
    private readonly BlockingCollection<IReadOnlyList<IObjectResolver>> _readRows;
    private readonly CancellationToken _token;
    private readonly Func<Exception?> _getParentException;

    public ChunkedSource(BlockingCollection<IReadOnlyList<IObjectResolver>> readRows, CancellationToken token, Func<Exception?> getParentException)
    {
        _readRows = readRows;
        _token = token;
        _getParentException = getParentException;
    }

    public IEnumerator<IObjectResolver> GetEnumerator()
    {
        return new ChunkEnumerator(_readRows, _token, _getParentException);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}