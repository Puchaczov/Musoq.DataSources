using Cocona;
using Cocona.Builder;

namespace Musoq.DataSources.Roslyn.CommandLineArguments;

public static class CoconaArguments
{
    public static void SetupArguments(ICoconaCommandsBuilder builder, Func<string[], Task<int>> invokeAsync)
    {
        builder.AddSubCommand("csharp", commandsBuilder =>
        {
            // ReSharper disable once VariableHidesOuterVariable
            commandsBuilder.AddSubCommand("solution", commandsBuilder =>
            {
                Delegate loadSolution = async (string path, string bucket) => await invokeAsync(["csharp", "solution", "load", "--solution-file-path", path, bucket]);
                Delegate unloadSolution = async (string path, string bucket) => await invokeAsync(["csharp", "solution", "unload", "--solution-file-path", path, bucket]);

                commandsBuilder.AddCommand("load", loadSolution).WithDescription("Loads solution to memory");
                commandsBuilder.AddCommand("unload", unloadSolution).WithDescription("Unload solution from memory");
            });
        });
    }
}