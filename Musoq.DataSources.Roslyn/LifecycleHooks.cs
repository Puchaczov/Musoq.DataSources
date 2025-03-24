using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ConsoleAppFramework;
using Microsoft.Build.Locator;
using Musoq.DataSources.Roslyn.CoconaCommands;

namespace Musoq.DataSources.Roslyn;

/// <summary>
/// Class that contains lifecycle hooks for C# data source.
/// </summary>
public static class LifecycleHooks
{
#pragma warning disable CA2255
    /// <summary>
    /// Initializes C# data source.
    /// </summary>
    [ModuleInitializer]
#pragma warning restore CA2255
    public static void Initialize()
    {
        if (MSBuildLocator.CanRegister)
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
    public static async Task<(int ReturnValue, Exception[] Exceptions)> LoadToMemoryAsync(string[] args, CancellationToken cancellationToken)
    {
        try
        {
            var app = ConsoleApp.Create();
        
            app.Add("solution load", async (string solutionFilePath) =>
            {
                var command = new SolutionOperationsCommand();
                await command.LoadAsync(solutionFilePath, cancellationToken);
            });

            await app.RunAsync(args);
            
            return (0, []);
        }
        catch (Exception e)
        {
            if (Debugger.IsAttached)
            {
                Debugger.Break();
            }

            return (-1, [e]);
        }
    }

    /// <summary>
    /// Unloads C# data source.
    /// </summary>
    /// <param name="args">Arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>0 if succeeded, otherwise error code</returns>
    public static async Task<(int ReturnValue, Exception[] Exceptions)> UnloadFromMemoryAsync(string[] args, CancellationToken cancellationToken)
    {   
        try
        {
            var app = ConsoleApp.Create();
        
            app.Add("solution unload", async (string solutionFilePath) =>
            {
                var command = new SolutionOperationsCommand();
                await command.LoadAsync(solutionFilePath, cancellationToken);
            });

            await app.RunAsync(args);
            
            return (0, []);
        }
        catch (Exception e)
        {
            if (Debugger.IsAttached)
            {
                Debugger.Break();
            }
            
            return (-1, [e]);
        }
    }

    /// <summary>
    /// Loads required dependencies.
    /// </summary>
    public static void LoadRequiredDependencies()
    {
    }
}