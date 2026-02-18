using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Musoq.DataSources.Roslyn.CliCommands;
using Musoq.DataSources.Roslyn.Components;
using Musoq.DataSources.Roslyn.Components.NuGet;
using Musoq.DataSources.Roslyn.Entities;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Roslyn.RowsSources;

internal sealed class CSharpInMemorySolutionRowsSource(
    SolutionEntity solution,
    IHttpClient? httpClient,
    IFileSystem? fileSystem,
    string? nugetPropertiesResolveEndpoint, 
    INuGetPropertiesResolver nuGetPropertiesResolver,
    ILogger logger, 
    RuntimeContext runtimeContext
)
    : CSharpSolutionRowsSourceBase(runtimeContext)
{
    protected override Task CollectChunksAsync(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource, CancellationToken cancellationToken)
    {
        var packageVersionConcurrencyManager = new PackageVersionConcurrencyManager();
        
        chunkedSource.Add(new List<IObjectResolver>
        {
            new EntityResolver<SolutionEntity>(
                solution.CloneWith(
                    new NuGetPackageMetadataRetriever(
                        new NuGetCachePathResolver(
                            solution.Path, 
                            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? 
                                OSPlatform.Windows : 
                                OSPlatform.Linux,
                            logger
                        ), 
                        nugetPropertiesResolveEndpoint,
                        new NuGetRetrievalService(
                            nuGetPropertiesResolver,
                            fileSystem,
                            httpClient), 
                        fileSystem,
                        packageVersionConcurrencyManager,
                        SolutionOperationsCommand.BannedPropertiesValues,
                        SolutionOperationsCommand.ResolveValueStrategy,
                        logger),
                    RuntimeContext.EndWorkToken
                ), SolutionEntity.NameToIndexMap, SolutionEntity.IndexToObjectAccessMap)
        }, cancellationToken);
        
        return Task.CompletedTask;
    }
}