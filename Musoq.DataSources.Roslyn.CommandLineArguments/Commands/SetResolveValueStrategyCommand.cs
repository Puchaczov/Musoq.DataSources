using System.Net.Http.Json;
using Musoq.DataSources.Roslyn.CommandLineArguments.Dtos;
using Musoq.DataSources.Roslyn.CommandLineArguments.Settings;
using Spectre.Console.Cli;

namespace Musoq.DataSources.Roslyn.CommandLineArguments.Commands;

public class SetResolveValueStrategyCommand : CliCommandBase<SetResolveValueStrategySettings>
{
    public override Task<int> ExecuteAsync(CommandContext context, SetResolveValueStrategySettings settings)
    {
        var dto = new SetBucketRequestDto
        {
            SchemaName = "csharp",
            Arguments =
            [
                "solution",
                "resolve",
                "value",
                "strategy",
                "set",
                "--value",
                settings.Strategy
            ]
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"bucket/set/{settings.Bucket}")
        {
            Content = JsonContent.Create(dto)
        };

        return InvokeAsync(context, request);
    }
}