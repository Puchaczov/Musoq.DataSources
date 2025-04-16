using Cocona;
using Cocona.Builder;

namespace Musoq.DataSources.Roslyn.CommandLineArguments;

public static class CoconaArguments
{
    public static void SetupArguments(ICoconaCommandsBuilder builder, Func<string?[], Task<int>> invokeAsync)
    {
        builder.AddSubCommand("csharp", commandsBuilder =>
        {
            // ReSharper disable once VariableHidesOuterVariable
            commandsBuilder.AddSubCommand("solution", commandsBuilder =>
            {
                Delegate loadSolution = async (string path, string bucket, string? cacheDirectory) => await invokeAsync(["csharp", "solution", "load", "--solution-file-path", path, "--bucket", bucket, "--cache-directory-path", cacheDirectory]);
                Delegate unloadSolution = async (string path, string bucket) => await invokeAsync(["csharp", "solution", "unload", "--solution-file-path", path, "--bucket", bucket]);

                commandsBuilder.AddCommand("load", loadSolution).WithDescription("Loads solution to memory");
                commandsBuilder.AddCommand("unload", unloadSolution).WithDescription("Unload solution from memory");
                commandsBuilder.AddSubCommand("cache", cacheCommandsBuilder =>
                {
                    Delegate clearCache = async (string bucket, string? cacheDirectory) => await invokeAsync(["csharp", "solution", "cache", "clear", "--bucket", bucket, "--cache-directory-path", cacheDirectory]);
                    
                    cacheCommandsBuilder.AddCommand("clear", clearCache).WithDescription("Clears NuGet cache");
                });
            });
        });
    }
}