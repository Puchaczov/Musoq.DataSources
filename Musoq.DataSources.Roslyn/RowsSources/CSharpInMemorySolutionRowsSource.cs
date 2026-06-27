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

internal sealed class CSharpInMemorySolutionRowsSource(
    SolutionEntity solution,
    IHttpClient? httpClient,
    IFileSystem? fileSystem,
    string? nugetPropertiesResolveEndpoint,
    INuGetPropertiesResolver nuGetPropertiesResolver,
    ILogger logger,
    SourceExecutionContext executionContext
)
    : CSharpSolutionRowsSourceBase(executionContext)
{
    protected override Task CollectChunksAsync(IChunkWriter<SolutionEntity> writer, CancellationToken cancellationToken)
    {
        var packageVersionConcurrencyManager = new PackageVersionConcurrencyManager();
        var filters = RoslynSourcePlanner.GetFilters(ExecutionContext.Plan);
        var acceptedPredicate = ExecutionContext.Plan.AcceptedPredicate;

        writer.Write([
            solution.CloneWith(
                new NuGetPackageMetadataRetriever(
                    new NuGetCachePathResolver(
                        solution.Path,
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
                    logger),
                ExecutionContext.EndWorkToken,
                project => RoslynSourcePlanner.Matches(filters, project) &&
                           RoslynSourcePlanner.Matches(acceptedPredicate, project))
        ]);

        return Task.CompletedTask;
    }
}
