using Spectre.Console.Cli;
using Musoq.DataSources.Roslyn.CommandLineArguments.Settings;

namespace Musoq.DataSources.Roslyn.CommandLineArguments.Commands;

public class LoadSolutionCommand : AsyncCommand<LoadSolutionSettings>
{
    private readonly Func<string, string?[], Task<int>> _invokeAsync;

    public LoadSolutionCommand(Func<string, string?[], Task<int>> invokeAsync)
    {
        _invokeAsync = invokeAsync;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, LoadSolutionSettings settings)
    {
        return await _invokeAsync("load", new[]
        {
            "csharp", "solution", "load",
            "--solution-file-path", settings.Path,
            "--bucket", settings.Bucket,
            "--cache-directory-path", settings.CacheDirectoryPath
        });
    }
}
