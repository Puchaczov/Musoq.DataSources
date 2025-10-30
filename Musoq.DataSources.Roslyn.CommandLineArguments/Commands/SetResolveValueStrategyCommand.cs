using System.Net.Http.Json;
using Spectre.Console.Cli;
using Musoq.DataSources.Roslyn.CommandLineArguments.Settings;
using Musoq.DataSources.Roslyn.CommandLineArguments.Dtos;

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
