using System.Net.Http.Json;
using Musoq.DataSources.Roslyn.CommandLineArguments.Dtos;
using Musoq.DataSources.Roslyn.CommandLineArguments.Settings;
using Spectre.Console.Cli;

namespace Musoq.DataSources.Roslyn.CommandLineArguments.Commands;

public class GetResolveValueStrategyCommand : CliCommandBase<BucketSettings>
{
    public override Task<int> ExecuteAsync(CommandContext context, BucketSettings settings)
    {
        var dto = new GetBucketRequestDto
        {
            SchemaName = "csharp",
            Arguments =
            [
                "solution",
                "resolve",
                "value",
                "strategy",
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