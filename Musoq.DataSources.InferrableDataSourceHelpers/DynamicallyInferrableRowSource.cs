using Musoq.DataSources.InferrableDataSourceHelpers.Components;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.InferrableDataSourceHelpers;

public abstract class DynamicallyInferrableRowSource<TInput> : GenericRowSource<TInput>
{
    private readonly IRowsSourceDetector<TInput> _rowsSourceDetector;
    private readonly RuntimeContext _context;

    protected DynamicallyInferrableRowSource(IRowsSourceDetector<TInput> rowsSourceDetector, RuntimeContext context)
    {
        _rowsSourceDetector = rowsSourceDetector;
        _context = context;
    }

    protected override async IAsyncEnumerable<TInput> GetDataAsync()
    {
        var reader = await _rowsSourceDetector.InferAsync(_context, typeof(TInput));
        
        while (await reader.MoveNextAsync())
            yield return reader.Current;
    }

    protected override IObjectResolver CreateResolver(TInput item)
    {
        return _rowsSourceDetector.Resolve(item, _context.EndWorkToken);
    }
}