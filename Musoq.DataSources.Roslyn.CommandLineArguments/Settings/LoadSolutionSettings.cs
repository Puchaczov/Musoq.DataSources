using System.ComponentModel;
using Spectre.Console.Cli;

namespace Musoq.DataSources.Roslyn.CommandLineArguments.Settings;

public class LoadSolutionSettings : CommandSettings
{
    [CommandArgument(0, "<path>")]
    [Description("Path to the solution file")]
    public string Path { get; set; } = string.Empty;

    [CommandArgument(1, "<bucket>")]
    [Description("Bucket identifier")]
    public string Bucket { get; set; } = string.Empty;

    [CommandOption("--cache-directory-path")]
    [Description("Optional cache directory path")]
    public string? CacheDirectoryPath { get; set; }
}
