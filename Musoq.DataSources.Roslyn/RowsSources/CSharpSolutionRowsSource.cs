using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.MSBuild;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.DataSources.Roslyn.Components;
using Musoq.DataSources.Roslyn.Entities;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Roslyn.RowsSources;

internal class CSharpSolutionRowsSource(string solutionFilePath, string? nugetPropertiesResolveEndpoint, CancellationToken endWorkToken) 
    : AsyncRowsSourceBase<SolutionEntity>(endWorkToken)
{
    protected override async Task CollectChunksAsync(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource, CancellationToken cancellationToken)
    {
        var workspace = MSBuildWorkspace.Create();
        var solution = await workspace.OpenSolutionAsync(solutionFilePath, cancellationToken: cancellationToken);
        var nuGetPackageMetadataRetriever = new NuGetPackageMetadataRetriever(new NuGetCachePathResolver(), nugetPropertiesResolveEndpoint);
        var solutionEntity = new SolutionEntity(solution, nuGetPackageMetadataRetriever, cancellationToken);
        
        await Parallel.ForEachAsync(solutionEntity.Projects, cancellationToken, async (project, token) =>
        {
            await Parallel.ForEachAsync(project.Documents, token, async (document, _) =>
            {
                await document.InitializeAsync();
            });
        });
        
        chunkedSource.Add(new List<IObjectResolver>
        {
            new EntityResolver<SolutionEntity>(solutionEntity, SolutionEntity.NameToIndexMap, SolutionEntity.IndexToObjectAccessMap)
        }, cancellationToken);
    }
}