using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;
using Musoq.DataSources.Roslyn.Components;
using Musoq.DataSources.Roslyn.Components.NuGet;
using Musoq.DataSources.Roslyn.Entities;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Roslyn.RowsSources;

internal class CSharpImmediateLoadSolutionRowsSource(
    string solutionFilePath,
    IHttpClient httpClient,
    IFileSystem fileSystem,
    string? nugetPropertiesResolveEndpoint, 
    INuGetPropertiesResolver nuGetPropertiesResolver,
    ILogger logger, 
    CancellationToken queryCancelledToken
)
    : CSharpSolutionRowsSourceBase(queryCancelledToken)
{
    private readonly CancellationToken _queryCancelledToken = queryCancelledToken;

    protected override async Task CollectChunksAsync(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource, CancellationToken cancellationToken)
    {
        logger.LogTrace("Loading solution file: {solutionFilePath}", solutionFilePath);
        
        var workspace = MSBuildWorkspace.Create();
        var solution = await workspace.OpenSolutionAsync(solutionFilePath, null, null, cancellationToken);
        var packageVersionConcurrencyManager = new PackageVersionConcurrencyManager();
        var nuGetPackageMetadataRetriever = new NuGetPackageMetadataRetriever(
            new NuGetCachePathResolver(
                solutionFilePath, 
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? 
                    OSPlatform.Windows : 
                    OSPlatform.Linux
            ), 
            nugetPropertiesResolveEndpoint,
            new NuGetRetrievalService(
                nuGetPropertiesResolver,
                fileSystem,
                httpClient),
            fileSystem,
            packageVersionConcurrencyManager,
            logger
        );
        var solutionEntity = new SolutionEntity(solution, nuGetPackageMetadataRetriever, _queryCancelledToken);
        
        logger.LogTrace("Initializing solution");
        
        await Parallel.ForEachAsync(solutionEntity.Projects, cancellationToken, async (project, token) =>
        {
            await Parallel.ForEachAsync(project.Documents, token, async (document, _) =>
            {
                await document.InitializeAsync();
            });
        });
        
        logger.LogTrace("Solution initialized.");
        
        chunkedSource.Add(new List<IObjectResolver>
        {
            new EntityResolver<SolutionEntity>(solutionEntity, SolutionEntity.NameToIndexMap, SolutionEntity.IndexToObjectAccessMap)
        }, cancellationToken);
    }
}