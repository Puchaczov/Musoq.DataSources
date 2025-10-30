using System.ComponentModel;
using Spectre.Console.Cli;

namespace Musoq.DataSources.Roslyn.CommandLineArguments.Settings;

public class SetResolveValueStrategySettings : CommandSettings
{
    [CommandArgument(0, "<bucket>")]
    [Description("Bucket identifier")]
    public string Bucket { get; set; } = string.Empty;

    [CommandOption("--value")]
    [Description("Resolve value strategy: UseNugetOrgApiOnly | UseCustomApiOnly | UseNugetOrgApiAndCustomApi")]
    public string? Strategy { get; set; }
}
