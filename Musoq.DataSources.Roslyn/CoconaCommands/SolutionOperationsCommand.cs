using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.MSBuild;
using Musoq.DataSources.Roslyn.Components;
using Musoq.DataSources.Roslyn.Components.NuGet;
using Musoq.DataSources.Roslyn.Entities;

namespace Musoq.DataSources.Roslyn.CoconaCommands;

internal class SolutionOperationsCommand
{
    public async Task LoadAsync(string solutionFilePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var workspace = MSBuildWorkspace.Create();
        var solution = await workspace.OpenSolutionAsync(solutionFilePath, cancellationToken: cancellationToken);
        var fileSystem = new DefaultFileSystem();
        var localResolver = new MusoqServerBasedPropertiesResolver();
        var nuGetPackageMetadataRetriever = new NuGetPackageMetadataRetriever(
            new NuGetCachePathResolver(solutionFilePath, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? OSPlatform.Windows : OSPlatform.Linux), 
            null,
            new NuGetRetrievalService(
                localResolver,
                fileSystem,
                new DefaultHttpClient()),
            fileSystem);
        var solutionEntity = new SolutionEntity(solution, nuGetPackageMetadataRetriever, cancellationToken);
        
        await Parallel.ForEachAsync(solutionEntity.Projects, cancellationToken, async (project, token) =>
        {
            await Parallel.ForEachAsync(project.Documents, token, async (document, _) =>
            {
                await document.InitializeAsync();
            });
        });

        CSharpSchema.Solutions.TryAdd(solutionFilePath, solutionEntity);
    }
    
    public Task UnloadAsync(string solutionFilePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        CSharpSchema.Solutions.TryRemove(solutionFilePath, out _);
            
        return Task.CompletedTask;
    }
}