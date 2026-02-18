using System.Net.Http.Json;
using Musoq.DataSources.Roslyn.CommandLineArguments.Dtos;
using Musoq.DataSources.Roslyn.CommandLineArguments.Settings;
using Spectre.Console.Cli;

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