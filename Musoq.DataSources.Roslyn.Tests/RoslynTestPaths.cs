using System.Runtime.InteropServices;

namespace Musoq.DataSources.Roslyn.Tests;

internal static class RoslynTestPaths
{
    public static string SampleSolution =>
        Path.Combine(AppContext.BaseDirectory, "TestsSolutions", "Solution1", "Solution1.sln");

    public static OSPlatform CurrentPlatform =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? OSPlatform.Windows : OSPlatform.Linux;
}
