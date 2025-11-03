using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;
using Musoq.DataSources.Roslyn.CliCommands;
using Musoq.DataSources.Roslyn.Components;
using Musoq.DataSources.Roslyn.Components.NuGet;
using Musoq.DataSources.Roslyn.Entities;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Roslyn.RowsSources;

internal class CSharpImmediateLoadSolutionRowsSource(
    string solutionFilePath,
    IHttpClient? httpClient,
    IFileSystem? fileSystem,
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
        cancellationToken.ThrowIfCancellationRequested();
        
        logger.LogTrace("Loading solution file: {solutionFilePath}", solutionFilePath);
        
        var workspace = MSBuildWorkspace.Create();
        var solutionLoadLogger = new SolutionLoadLogger(logger);
        var projectLoadProgressLogger = new ProjectLoadProgressLogger(logger);
        var solution = await workspace.OpenSolutionAsync(solutionFilePath, solutionLoadLogger, projectLoadProgressLogger, cancellationToken);
        var packageVersionConcurrencyManager = new PackageVersionConcurrencyManager();
        var nuGetPackageMetadataRetriever = new NuGetPackageMetadataRetriever(
            new NuGetCachePathResolver(
                solutionFilePath, 
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
            logger
        );
        var solutionEntity = new SolutionEntity(solution, nuGetPackageMetadataRetriever, _queryCancelledToken);
        
        logger.LogTrace("Initializing solution");
        
        await Parallel.ForEachAsync(solutionEntity.Projects, cancellationToken, async (project, token) =>
        {
            foreach (var document in project.Documents)
            {
                await document.InitializeAsync(token);
            }
        });
        
        logger.LogTrace("Solution initialized.");
        
        chunkedSource.Add(new List<IObjectResolver>
        {
            new EntityResolver<SolutionEntity>(solutionEntity, SolutionEntity.NameToIndexMap, SolutionEntity.IndexToObjectAccessMap)
        }, cancellationToken);
    }
}