using System.Net.Http.Json;
using Musoq.DataSources.Roslyn.CommandLineArguments.Dtos;
using Musoq.DataSources.Roslyn.CommandLineArguments.Settings;
using Spectre.Console.Cli;

namespace Musoq.DataSources.Roslyn.CommandLineArguments.Commands;

public class UnloadSolutionCommand : CliCommandBase<UnloadSolutionSettings>
{
    public override Task<int> ExecuteAsync(CommandContext context, UnloadSolutionSettings settings)
    {
        var dto = new UnloadBucketRequestDto
        {
            SchemaName = "csharp",
            Arguments =
            [
                "solution",
                "unload",
                "--solution-file-path",
                settings.Path
            ]
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"bucket/unload/{settings.Bucket}")
        {
            Content = JsonContent.Create(dto)
        };

        return InvokeAsync(context, request);
    }
}