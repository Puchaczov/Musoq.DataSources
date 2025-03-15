using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.MSBuild;

namespace Musoq.DataSources.Roslyn.CoconaCommands;

internal class SolutionOperationsCommand
{
    public async Task LoadAsync(string solutionFilePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var workspace = MSBuildWorkspace.Create();
        var solution = await workspace.OpenSolutionAsync(solutionFilePath, cancellationToken: cancellationToken);
        
        await Parallel.ForEachAsync(solution.Projects, cancellationToken, async (project, outerToken) =>
        {
            await Parallel.ForEachAsync(project.Documents, outerToken, async (document, innerToken) =>
            {
                await document.GetSyntaxTreeAsync(innerToken);
                await document.GetSemanticModelAsync(innerToken);
            });
        });

        CSharpSchema.Solutions.TryAdd(solutionFilePath, solution);
    }
    
    public Task UnloadAsync(string solutionFilePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        CSharpSchema.Solutions.TryRemove(solutionFilePath, out _);
            
        return Task.CompletedTask;
    }
}