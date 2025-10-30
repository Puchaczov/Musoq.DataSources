using System.Net.Http.Json;
using Spectre.Console.Cli;
using Musoq.DataSources.Roslyn.CommandLineArguments.Settings;
using Musoq.DataSources.Roslyn.CommandLineArguments.Dtos;

namespace Musoq.DataSources.Roslyn.CommandLineArguments.Commands;

public class GetCacheCommand : CliCommandBase<BucketSettings>
{
    public override Task<int> ExecuteAsync(CommandContext context, BucketSettings settings)
    {
        var dto = new GetBucketRequestDto
        {
            SchemaName = "csharp",
            Arguments =
            [
                "solution",
                "cache",
                "get"
            ]
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"bucket/get/{settings.Bucket}")
        {
            Content = JsonContent.Create(dto)
        };

        return InvokeAsync(context, request);
    }
}