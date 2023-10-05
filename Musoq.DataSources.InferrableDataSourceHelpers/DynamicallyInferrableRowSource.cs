using System.Runtime.CompilerServices;
using Musoq.DataSources.InferrableDataSourceHelpers.Components;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.InferrableDataSourceHelpers;

public abstract class DynamicallyInferrableRowSource<TInput> : GenericRowSource<TInput>
{
    private readonly IRowsSourceDetector<TInput> _rowsSourceDetector;
    private readonly RuntimeContext _context;

    protected DynamicallyInferrableRowSource(IRowsSourceDetector<TInput> rowsSourceDetector, RuntimeContext context)
        : base(context.EndWorkToken)
    {
        _rowsSourceDetector = rowsSourceDetector;
        _context = context;
    }

    protected override async IAsyncEnumerable<TInput> GetDataAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var reader = await _rowsSourceDetector.InferAsync(_context.QueryInformation, typeof(TInput), cancellationToken);
        
        while (await reader.MoveNextAsync())
            yield return reader.Current;
    }

    protected override IObjectResolver CreateResolver(TInput item)
    {
        return _rowsSourceDetector.Resolve(item, _context.EndWorkToken);
    }
}