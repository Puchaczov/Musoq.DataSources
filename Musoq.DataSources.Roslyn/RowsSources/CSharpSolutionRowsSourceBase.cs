using Musoq.DataSources.AsyncRowsSource;
using Musoq.DataSources.Roslyn.Entities;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Roslyn.RowsSources;

internal abstract class CSharpSolutionRowsSourceBase(SourceExecutionContext executionContext)
    : AsyncRowsSourceBase<SolutionEntity>(executionContext.EndWorkToken)
{
    protected readonly SourceExecutionContext ExecutionContext = executionContext;
}
