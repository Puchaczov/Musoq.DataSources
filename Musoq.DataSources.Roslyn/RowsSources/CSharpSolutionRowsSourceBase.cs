using Musoq.DataSources.AsyncRowsSource;
using Musoq.DataSources.Roslyn.Entities;
using Musoq.Schema;

namespace Musoq.DataSources.Roslyn.RowsSources;

internal abstract class CSharpSolutionRowsSourceBase(RuntimeContext runtimeContext)
    : AsyncRowsSourceBase<SolutionEntity>(runtimeContext.EndWorkToken)
{
    protected readonly RuntimeContext RuntimeContext = runtimeContext;
}