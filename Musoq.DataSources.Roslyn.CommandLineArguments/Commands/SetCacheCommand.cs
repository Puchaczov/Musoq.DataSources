using System.Net.Http.Json;
using Spectre.Console.Cli;
using Musoq.DataSources.Roslyn.CommandLineArguments.Settings;
using Musoq.DataSources.Roslyn.CommandLineArguments.Dtos;

namespace Musoq.DataSources.Roslyn.CommandLineArguments.Commands;

public class SetCacheCommand : CliCommandBase<CacheBucketSettings>
{
    public override Task<int> ExecuteAsync(CommandContext context, CacheBucketSettings settings)
    {
        var dto = new SetBucketRequestDto
        {
            SchemaName = "csharp",
            Arguments =
            [
                "solution",
                "cache",
                "set",
                "--cache-directory-path",
                settings.CacheDirectoryPath
            ]
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"bucket/set/{settings.Bucket}")
        {
            Content = JsonContent.Create(dto)
        };

        return InvokeAsync(context, request);
    }
}
