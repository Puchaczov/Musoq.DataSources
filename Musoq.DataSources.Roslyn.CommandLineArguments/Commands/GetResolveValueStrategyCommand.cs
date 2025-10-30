using Spectre.Console.Cli;
using Musoq.DataSources.Roslyn.CommandLineArguments.Settings;

namespace Musoq.DataSources.Roslyn.CommandLineArguments.Commands;

public class GetResolveValueStrategyCommand : AsyncCommand<BucketSettings>
{
    private readonly Func<string, string?[], Task<int>> _invokeAsync;

    public GetResolveValueStrategyCommand(Func<string, string?[], Task<int>> invokeAsync)
    {
        _invokeAsync = invokeAsync;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, BucketSettings settings)
    {
        return await _invokeAsync("get", new[]
        {
            "csharp", "solution", "resolve", "value", "strategy", "get",
            "--bucket", settings.Bucket
        });
    }
}
