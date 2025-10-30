using System.Net.Http.Json;
using Spectre.Console.Cli;
using Musoq.DataSources.Roslyn.CommandLineArguments.Settings;
using Musoq.DataSources.Roslyn.CommandLineArguments.Dtos;

namespace Musoq.DataSources.Roslyn.CommandLineArguments.Commands;

public class LoadSolutionCommand : CliCommandBase<LoadSolutionSettings>
{
    public override Task<int> ExecuteAsync(CommandContext context, LoadSolutionSettings settings)
    {
        var dto = new LoadBucketRequestDto
        {
            SchemaName = "csharp",
            Arguments =
            [
                "solution",
                "load",
                "--solution-file-path",
                settings.Path,
                "--cache-directory-path",
                settings.CacheDirectoryPath
            ]
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"bucket/load/{settings.Bucket}")
        {
            Content = JsonContent.Create(dto)
        };

        return InvokeAsync(context, request);
    }
}
