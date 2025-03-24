using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.DataSources.Roslyn.Components;
using Musoq.DataSources.Roslyn.Components.NuGet;
using Musoq.DataSources.Roslyn.Entities;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Roslyn.RowsSources;

internal sealed class CSharpInMemorySolutionRowsSource(SolutionEntity solution, string? nugetPropertiesResolveEndpoint, INuGetPropertiesResolver nuGetPropertiesResolver, CancellationToken queryCancelledToken)
    : AsyncRowsSourceBase<SolutionEntity>(queryCancelledToken)
{
    private readonly CancellationToken _queryCancelledToken = queryCancelledToken;

    protected override Task CollectChunksAsync(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource, CancellationToken cancellationToken)
    {
        var fileSystem = new DefaultFileSystem();
        chunkedSource.Add(new List<IObjectResolver>
        {
            new EntityResolver<SolutionEntity>(
                solution.CloneWith(
                    new NuGetPackageMetadataRetriever(
                        new NuGetCachePathResolver(solution.Path, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? OSPlatform.Windows : OSPlatform.Linux), 
                        nugetPropertiesResolveEndpoint, 
                        new NuGetRetrievalService(
                            nuGetPropertiesResolver,
                            fileSystem,
                            new DefaultHttpClient()), 
                        fileSystem),
                    _queryCancelledToken
                ), SolutionEntity.NameToIndexMap, SolutionEntity.IndexToObjectAccessMap)
        }, cancellationToken);
        return Task.CompletedTask;
    }
}