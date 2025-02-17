using System;
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

internal sealed class CSharpInMemorySolutionRowsSource(SolutionEntity solution, string? nugetPropertiesResolveEndpoint, CancellationToken endWorkToken)
    : AsyncRowsSourceBase<SolutionEntity>(endWorkToken)
{
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
                            fileSystem,
                            new DefaultHttpClient()), 
                        fileSystem),
                    cancellationToken
                ), SolutionEntity.NameToIndexMap, SolutionEntity.IndexToObjectAccessMap)
        }, cancellationToken);
        return Task.CompletedTask;
    }
}