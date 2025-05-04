using Cocona;
using Cocona.Builder;

namespace Musoq.DataSources.Roslyn.CommandLineArguments;

public static class CoconaArguments
{
    public static void SetupArguments(ICoconaCommandsBuilder builder, Func<string, string?[], Task<int>> invokeAsync)
    {
        builder.AddSubCommand("csharp", commandsBuilder =>
        {
            // ReSharper disable once VariableHidesOuterVariable
            commandsBuilder.AddSubCommand("solution", commandsBuilder =>
            {
                Delegate loadSolution = async (string path, string bucket, string? cacheDirectoryPath) => await invokeAsync("load", ["csharp", "solution", "load", "--solution-file-path", path, "--bucket", bucket, "--cache-directory-path", cacheDirectoryPath]);
                Delegate unloadSolution = async (string path, string bucket) => await invokeAsync("unload", ["csharp", "solution", "unload", "--solution-file-path", path, "--bucket", bucket]);

                commandsBuilder.AddCommand("load", loadSolution).WithDescription("Loads solution to memory");
                commandsBuilder.AddCommand("unload", unloadSolution).WithDescription("Unload solution from memory");
                commandsBuilder.AddSubCommand("cache", cacheCommandsBuilder =>
                {
                    Delegate clearCache = async (string bucket, string? cacheDirectoryPath) => await invokeAsync("set", ["csharp", "solution", "cache", "clear", "--bucket", bucket, "--cache-directory-path", cacheDirectoryPath]);
                    Delegate getCache = async (string bucket) =>
                        await invokeAsync("get", ["csharp", "solution", "cache", "get", "--bucket", bucket]);
                    Delegate setCache = async (string bucket, string? cacheDirectoryPath) => await invokeAsync("set", [
                        "csharp", "solution", "cache", "set", "--bucket", bucket, "--cache-directory-path",
                        cacheDirectoryPath
                    ]);
                    
                    cacheCommandsBuilder.AddCommand("clear", clearCache).WithDescription("Clears cache directory");
                    cacheCommandsBuilder.AddCommand("get", getCache).WithDescription("Gets cache directory path");
                    cacheCommandsBuilder.AddCommand("set", setCache).WithAliases("Sets cache directory path");
                });

                commandsBuilder.AddSubCommand("resolve-value-strategy", cacheCommandsBuilder =>
                {
                    Delegate getResolveValueStrategy = async (string bucket) =>
                        await invokeAsync("get", ["csharp", "solution", "resolve", "value", "strategy", "get", "--bucket", bucket]);
                    Delegate setResolveValueStrategy = async (string bucket, string? strategy) =>
                        await invokeAsync("set", ["csharp", "solution", "resolve", "value", "strategy", "set", "--bucket", bucket, "--value", strategy]);
                    
                    cacheCommandsBuilder.AddCommand("get", getResolveValueStrategy).WithDescription("Gets resolve value strategy. Will be equal to UseNugetOrgApiOnly | UseCustomApiOnly | UseNugetOrgApiAndCustomApi");
                    cacheCommandsBuilder.AddCommand("set", setResolveValueStrategy).WithDescription("Sets resolve value strategy. Must be equal to UseNugetOrgApiOnly | UseCustomApiOnly | UseNugetOrgApiAndCustomApi");
                });
            });
        });
    }
}