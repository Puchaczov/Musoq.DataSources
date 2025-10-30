using Spectre.Console.Cli;
using Musoq.DataSources.Roslyn.CommandLineArguments.Settings;

namespace Musoq.DataSources.Roslyn.CommandLineArguments.Commands;

public class SetCacheCommand : AsyncCommand<CacheBucketSettings>
{
    private readonly Func<string, string?[], Task<int>> _invokeAsync;

    public SetCacheCommand(Func<string, string?[], Task<int>> invokeAsync)
    {
        _invokeAsync = invokeAsync;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, CacheBucketSettings settings)
    {
        return await _invokeAsync("set", new[]
        {
            "csharp", "solution", "cache", "set",
            "--bucket", settings.Bucket,
            "--cache-directory-path", settings.CacheDirectoryPath
        });
    }
}
