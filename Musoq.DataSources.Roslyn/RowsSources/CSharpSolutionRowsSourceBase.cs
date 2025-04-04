using System.Threading;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.DataSources.Roslyn.Entities;

namespace Musoq.DataSources.Roslyn.RowsSources;

internal abstract class CSharpSolutionRowsSourceBase(CancellationToken cancellationToken) 
    : AsyncRowsSourceBase<SolutionEntity>(cancellationToken);