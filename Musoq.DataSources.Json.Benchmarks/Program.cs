using System.Diagnostics;
using BenchmarkDotNet.Running;

namespace Musoq.DataSources.Json.Benchmarks;

internal static class Program
{
    public static int Main(string[] args)
    {
        if (JsonBenchmarkDataCommand.TryRun(args, out var exitCode))
            return exitCode;

        if (args is ["profile-source"])
        {
            var benchmark = new JsonLegacySourceBenchmarks { RowCount = 100_000 };
            benchmark.Setup();
            return RunProfile("json-source", benchmark.LegacyDataSource);
        }

        if (args is ["profile-compiled"])
        {
            var benchmark = new JsonCompiledExecutionBenchmarks
            {
                RowCount = 100_000,
                QueryShape = JsonQueryShape.FullRow
            };
            benchmark.Setup();
            return RunProfile("json-compiled-full-row", benchmark.RunCompiledQuery);
        }

        if (args is ["smoke"])
        {
            foreach (var shape in Enum.GetValues<JsonQueryShape>())
            {
                var benchmark = new JsonCompiledExecutionBenchmarks
                {
                    RowCount = 10_000,
                    QueryShape = shape
                };
                benchmark.Setup();
                Console.WriteLine($"{shape}: {benchmark.RunCompiledQuery()}");
            }

            return 0;
        }

        if (args is ["verify"])
        {
            Console.WriteLine(JsonBenchmarkData.VerifyFixtures(10_000));
            return 0;
        }

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        return 0;
    }

    private static int RunProfile(string name, Func<long> operation)
    {
        var stopwatch = Stopwatch.StartNew();
        var iterations = 0;
        long checksum = 0;

        do
        {
            checksum = unchecked(checksum * 31 + operation());
            iterations++;
        } while (stopwatch.Elapsed < TimeSpan.FromSeconds(5));

        Console.WriteLine($"{name}: iterations={iterations}, checksum={checksum}");
        return 0;
    }
}
