using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.InferrableDataSourceHelpers.Components;

public interface IRowsSourceDetector<TInput>
{
    Task<IRowsReader<TInput>> InferAsync(RuntimeContext context, Type inputType);
    
    IObjectResolver Resolve(TInput item, CancellationToken cancellationToken);
}