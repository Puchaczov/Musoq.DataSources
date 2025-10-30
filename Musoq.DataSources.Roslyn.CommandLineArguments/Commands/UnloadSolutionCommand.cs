using Spectre.Console.Cli;
using Musoq.DataSources.Roslyn.CommandLineArguments.Settings;

namespace Musoq.DataSources.Roslyn.CommandLineArguments.Commands;

public class UnloadSolutionCommand : AsyncCommand<UnloadSolutionSettings>
{
    private readonly Func<string, string?[], Task<int>> _invokeAsync;

    public UnloadSolutionCommand(Func<string, string?[], Task<int>> invokeAsync)
    {
        _invokeAsync = invokeAsync;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, UnloadSolutionSettings settings)
    {
        return await _invokeAsync("unload", new[]
        {
            "csharp", "solution", "unload",
            "--solution-file-path", settings.Path,
            "--bucket", settings.Bucket
        });
    }
}
