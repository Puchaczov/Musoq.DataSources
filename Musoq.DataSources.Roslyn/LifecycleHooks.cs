using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ConsoleAppFramework;
using Microsoft.Build.Locator;
using Microsoft.Extensions.Logging;
using Musoq.DataSources.Roslyn.CoconaCommands;

namespace Musoq.DataSources.Roslyn;

/// <summary>
/// Class that contains lifecycle hooks for C# data source.
/// </summary>
public static class LifecycleHooks
{
    /// <summary>
    /// Logger for C# data source.
    /// </summary>
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public static ILogger? Logger { get; set; }
    
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
        
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                Logger?.LogError(exception, "Unhandled exception in AppDomain");
            }
        };
        
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Logger?.LogError(args.Exception, "Unobserved task exception");
            
            args.SetObserved();
        };
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
        
            app.Add("solution load", async (string solutionFilePath, string? cacheDirectoryPath) =>
            {
                if (cacheDirectoryPath is not null)
                {
                    SolutionOperationsCommand.DefaultCacheDirectoryPath = cacheDirectoryPath;
                }
                
                var command = new SolutionOperationsCommand(Logger ?? throw new NullReferenceException(nameof(Logger)));
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
    /// Sets the value of the specified property.
    /// </summary>
    /// <param name="args">Arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>0 if succeeded, otherwise error code</returns>
    /// <exception cref="NullReferenceException">Thrown when the logger is null.</exception>
    public static async Task<(int ReturnValue, Exception[] Exceptions)> SetAsync(string[] args, CancellationToken cancellationToken)
    {
        try
        {
            var app = ConsoleApp.Create();
            
            app.Add("solution cache clear", async (string? cacheDirectoryPath) =>
            {
                var finalCacheDirectoryPath = cacheDirectoryPath ?? SolutionOperationsCommand.DefaultCacheDirectoryPath;
                
                var command = new SolutionOperationsCommand(Logger ?? throw new NullReferenceException(nameof(Logger)));
                await command.ClearCacheAsync(
                    finalCacheDirectoryPath, 
                    cancellationToken);
            });
            
            app.Add("solution cache set",(string cacheDirectoryPath) =>
            {
                var command = new SolutionOperationsCommand(Logger ?? throw new NullReferenceException(nameof(Logger)));
                command.SetCacheDirectoryPath(cacheDirectoryPath);
            });
            
            app.Add("solution resolve value strategy set", (string value) =>
            {
                var command = new SolutionOperationsCommand(Logger ?? throw new NullReferenceException(nameof(Logger)));

                command.SetResolveValueStrategy(value);
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
    /// Sets the value of the specified property.
    /// </summary>
    /// <param name="args">Arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>0 if succeeded, otherwise error code</returns>
    /// <exception cref="NullReferenceException">Thrown when the logger is null.</exception>
    public static async Task<(int ReturnValue, Exception[] Exceptions, string? Value)> GetAsync(string[] args, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        try
        {
            var app = ConsoleApp.Create();
            string? value = null;
            
            app.Add("solution cache get", () =>
            {
                var command = new SolutionOperationsCommand(Logger ?? throw new NullReferenceException(nameof(Logger)));

                value = command.GetCacheDirectoryPath();
            });
            
            app.Add("solution resolve value strategy get", () =>
            {
                var command = new SolutionOperationsCommand(Logger ?? throw new NullReferenceException(nameof(Logger)));

                value = command.GetResolveValueStrategy();
            });

            await app.RunAsync(args);
            
            return (0, [], value);
        }
        catch (Exception e)
        {
            if (Debugger.IsAttached)
            {
                Debugger.Break();
            }

            return (-1, [e], null);
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
                var command = new SolutionOperationsCommand(Logger ?? throw new NullReferenceException(nameof(Logger)));
                await command.UnloadAsync(solutionFilePath, cancellationToken);
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