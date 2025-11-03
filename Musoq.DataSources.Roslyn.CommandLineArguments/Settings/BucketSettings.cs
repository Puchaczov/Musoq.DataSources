using System.ComponentModel;
using Spectre.Console.Cli;

namespace Musoq.DataSources.Roslyn.CommandLineArguments.Settings;

public class BucketSettings : CommandSettings
{
    [CommandArgument(0, "<bucket>")]
    [Description("Bucket identifier")]
    public string Bucket { get; set; } = string.Empty;
}
