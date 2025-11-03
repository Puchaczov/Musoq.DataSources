using Spectre.Console.Cli;
using Musoq.DataSources.Roslyn.CommandLineArguments.Commands;

namespace Musoq.DataSources.Roslyn.CommandLineArguments;

public static class SpectreArguments
{
    public static void ConfigureCommands(IConfigurator config, Func<HttpRequestMessage, Task<int>> invokeAsync)
    {
        config.AddBranch("csharp", csharp =>
        {
            csharp.AddBranch("solution", solution =>
            {
                solution.AddCommand<LoadSolutionCommand>("load")
                    .WithDescription("Loads solution to memory")
                    .WithData(invokeAsync);

                solution.AddCommand<UnloadSolutionCommand>("unload")
                    .WithDescription("Unload solution from memory")
                    .WithData(invokeAsync);

                solution.AddBranch("cache", cache =>
                {
                    cache.AddCommand<ClearCacheCommand>("clear")
                        .WithDescription("Clears cache directory")
                        .WithData(invokeAsync);

                    cache.AddCommand<GetCacheCommand>("get")
                        .WithDescription("Gets cache directory path")
                        .WithData(invokeAsync);

                    cache.AddCommand<SetCacheCommand>("set")
                        .WithDescription("Sets cache directory path")
                        .WithData(invokeAsync);
                });

                solution.AddBranch("resolve-value-strategy", resolveValueStrategy =>
                {
                    resolveValueStrategy.AddCommand<GetResolveValueStrategyCommand>("get")
                        .WithDescription("Gets resolve value strategy. Will be equal to UseNugetOrgApiOnly | UseCustomApiOnly | UseNugetOrgApiAndCustomApi")
                        .WithData(invokeAsync);

                    resolveValueStrategy.AddCommand<SetResolveValueStrategyCommand>("set")
                        .WithDescription("Sets resolve value strategy. Must be equal to UseNugetOrgApiOnly | UseCustomApiOnly | UseNugetOrgApiAndCustomApi")
                        .WithData(invokeAsync);
                });
            });
        });
    }
}
