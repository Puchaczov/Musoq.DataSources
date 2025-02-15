using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.DataSources.Roslyn.Components;
using Musoq.DataSources.Roslyn.Components.NuGet;
using Musoq.DataSources.Roslyn.Entities;
using Musoq.DataSources.Roslyn.Services;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Roslyn.RowsSources;

internal sealed class CSharpInMemorySolutionRowsSource(SolutionEntity solution, string? nugetPropertiesResolveEndpoint, CancellationToken endWorkToken)
    : AsyncRowsSourceBase<SolutionEntity>(endWorkToken)
{
    protected override Task CollectChunksAsync(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource, CancellationToken cancellationToken)
    {
        chunkedSource.Add(new List<IObjectResolver>
        {
            new EntityResolver<SolutionEntity>(
                solution.CloneWith(
                    new NuGetPackageMetadataRetriever(
                        new NuGetCachePathResolver(), 
                        nugetPropertiesResolveEndpoint, 
                        new NuGetRetrievalService(
                            new DefaultFileSystem(),
                            new DefaultHttpClient())),
                    cancellationToken
                ), SolutionEntity.NameToIndexMap, SolutionEntity.IndexToObjectAccessMap)
        }, cancellationToken);
        return Task.CompletedTask;
    }
}