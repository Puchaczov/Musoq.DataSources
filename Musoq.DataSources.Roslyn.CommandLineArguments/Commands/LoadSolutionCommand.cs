using System.Net.Http.Json;
using Musoq.DataSources.Roslyn.CommandLineArguments.Dtos;
using Musoq.DataSources.Roslyn.CommandLineArguments.Settings;
using Spectre.Console.Cli;

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