using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.MSBuild;
using Musoq.DataSources.Roslyn.Entities;

namespace Musoq.DataSources.Roslyn;

/// <summary>
/// Class that contains lifecycle hooks for C# data source.
/// </summary>
public static class CSharpLifecycleHooks
{
#pragma warning disable CA2255
    /// <summary>
    /// Initializes C# data source.
    /// </summary>
    [ModuleInitializer]
#pragma warning restore CA2255
    public static void Initialize()
    {
        if (Debugger.IsAttached)
        {
            Debugger.Break();
        }
        
        if (MSBuildLocator.IsRegistered == false)
        {
            MSBuildLocator.RegisterDefaults();
        }
    }
    
    /// <summary>
    /// Loads C# data source.
    /// </summary>
    /// <param name="args">Arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>0 if succeeded, otherwise error code</returns>
    public static async Task<int> LoadSolutionAsync(string[] args, CancellationToken cancellationToken)
    {
        if (Debugger.IsAttached)
        {
            Debugger.Break();            
        }
        
        var solutionFilePath = args[0];
        var workspace = MSBuildWorkspace.Create();
        var solution = await workspace.OpenSolutionAsync(solutionFilePath, cancellationToken: cancellationToken);
        var solutionEntity = new SolutionEntity(solution);
        
        await Parallel.ForEachAsync(solutionEntity.Projects, cancellationToken, async (project, token) =>
        {
            await Parallel.ForEachAsync(project.Documents, token, async (document, _) =>
            {
                await document.InitializeAsync();
            });
        });

        CSharpSchema.Solutions.TryAdd(solutionFilePath, solutionEntity);
        
        return 0;
    }

    /// <summary>
    /// Unloads C# data source.
    /// </summary>
    /// <param name="args">Arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>0 if succeeded, otherwise error code</returns>
    public static async Task<int> UnloadSolutionAsync(string[] args, CancellationToken cancellationToken)
    {
        return 0;
    }

    /// <summary>
    /// Loads required dependencies.
    /// </summary>
    public static void LoadRequiredDependencies()
    {
    }
}