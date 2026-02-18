using System.ComponentModel;
using Spectre.Console.Cli;

namespace Musoq.DataSources.Roslyn.CommandLineArguments.Settings;

public class CacheBucketSettings : CommandSettings
{
    [CommandArgument(0, "<bucket>")]
    [Description("Bucket identifier")]
    public string Bucket { get; set; } = string.Empty;

    [CommandOption("--cache-directory-path")]
    [Description("Optional cache directory path")]
    public string? CacheDirectoryPath { get; set; }
}