using Spectre.Console.Cli;
using Musoq.DataSources.Roslyn.CommandLineArguments.Settings;

namespace Musoq.DataSources.Roslyn.CommandLineArguments.Commands;

public class SetResolveValueStrategyCommand : AsyncCommand<SetResolveValueStrategySettings>
{
    private readonly Func<string, string?[], Task<int>> _invokeAsync;

    public SetResolveValueStrategyCommand(Func<string, string?[], Task<int>> invokeAsync)
    {
        _invokeAsync = invokeAsync;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, SetResolveValueStrategySettings settings)
    {
        return await _invokeAsync("set", new[]
        {
            "csharp", "solution", "resolve", "value", "strategy", "set",
            "--bucket", settings.Bucket,
            "--value", settings.Strategy
        });
    }
}
