using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.DataSources.Roslyn.Entities;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Roslyn.RowsSources;

internal sealed class CSharpInMemorySolutionRowsSource(SolutionEntity solution, CancellationToken endWorkToken)
    : AsyncRowsSourceBase<SolutionEntity>(endWorkToken)
{
    protected override Task CollectChunksAsync(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource, CancellationToken cancellationToken)
    {
        chunkedSource.Add(new List<IObjectResolver> { new EntityResolver<SolutionEntity>(solution, SolutionEntity.NameToIndexMap, SolutionEntity.IndexToObjectAccessMap) }, cancellationToken);
        return Task.CompletedTask;
    }
}