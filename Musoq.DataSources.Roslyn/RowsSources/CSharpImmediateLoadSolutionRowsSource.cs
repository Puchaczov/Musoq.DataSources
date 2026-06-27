using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Musoq.DataSources.Roslyn;
using Musoq.DataSources.Roslyn.CliCommands;
using Musoq.DataSources.Roslyn.Components;
using Musoq.DataSources.Roslyn.Components.NuGet;
using Musoq.DataSources.Roslyn.Entities;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Roslyn.RowsSources;

internal class CSharpImmediateLoadSolutionRowsSource(
    string solutionFilePath,
    IHttpClient? httpClient,
    IFileSystem? fileSystem,
    string? nugetPropertiesResolveEndpoint,
    INuGetPropertiesResolver nuGetPropertiesResolver,
    ILogger logger,
    SourceExecutionContext executionContext
)
    : CSharpSolutionRowsSourceBase(executionContext)
{
    protected override async Task CollectChunksAsync(IChunkWriter<SolutionEntity> writer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        logger.LogTrace("Loading solution file: {solutionFilePath}", solutionFilePath);

        var solution = await RoslynSolutionLoader.OpenSolutionAsync(solutionFilePath, logger, cancellationToken);
        var packageVersionConcurrencyManager = new PackageVersionConcurrencyManager();
        var filters = RoslynSourcePlanner.GetFilters(ExecutionContext.Plan);
        var acceptedPredicate = ExecutionContext.Plan.AcceptedPredicate;
        var nuGetPackageMetadataRetriever = new NuGetPackageMetadataRetriever(
            new NuGetCachePathResolver(
                solutionFilePath,
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? OSPlatform.Windows : OSPlatform.Linux,
                logger),
            nugetPropertiesResolveEndpoint,
            new NuGetRetrievalService(
                nuGetPropertiesResolver,
                fileSystem,
                httpClient),
            fileSystem,
            packageVersionConcurrencyManager,
            SolutionOperationsCommand.BannedPropertiesValues,
            SolutionOperationsCommand.ResolveValueStrategy,
            logger);
        var solutionEntity = new SolutionEntity(
            solution,
            nuGetPackageMetadataRetriever,
            ExecutionContext.EndWorkToken,
            project => RoslynSourcePlanner.Matches(filters, project) &&
                       RoslynSourcePlanner.Matches(acceptedPredicate, project));

        logger.LogTrace("Initializing solution");

        await Parallel.ForEachAsync(solutionEntity.Projects, cancellationToken, async (project, token) =>
        {
            foreach (var document in project.Documents)
                await document.InitializeAsync(token);
        });

        logger.LogTrace("Solution initialized.");

        writer.Write([solutionEntity]);
    }
}
