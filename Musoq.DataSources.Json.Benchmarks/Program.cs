using System.Diagnostics;
using BenchmarkDotNet.Running;

namespace Musoq.DataSources.Json.Benchmarks;

internal static class Program
{
    public static int Main(string[] args)
    {
        if (args is ["measure-native", var output])
            return NativeExecutionMeasurements.Run(output);

        if (JsonBenchmarkDataCommand.TryRun(args, out var exitCode))
            return exitCode;

        if (args is ["inspect-typed-execution", var directory])
        {
            Directory.CreateDirectory(directory);
            foreach (var typed in new[] { true })
            foreach (var shape in Enum.GetValues<JsonQueryShape>())
            {
                var benchmark = new JsonCompiledExecutionBenchmarks { RowCount = 100_000, QueryShape = shape, TypedSource = typed };
                benchmark.Setup();
                File.WriteAllText(Path.Combine(directory, $"{shape}-{typed}.cs"), benchmark.InspectGeneratedExecution());
            }
            var structuredPath = Path.GetFullPath(Path.Combine(directory, "structured-nullable.json"));
            File.WriteAllText(structuredPath, "[{\"Id\":1,\"Values\":[1,2]},{\"Id\":2,\"Values\":null},{\"Id\":3}]");
            var sourcePath = structuredPath.Replace('\\', '/').Replace("'", "''", StringComparison.Ordinal);
            var structuredQuery = $"select Id, JsonText(Values) as Json, Length(Values) as Size, MakeFlat(Values) as Flat from json.file('{sourcePath}') order by Id";
            File.WriteAllText(Path.Combine(directory, "StructuredNullable-True.cs"), JsonCompiledExecutionBenchmarks.InspectNativeQuery(structuredQuery));
            return 0;
        }

        if (args is ["profile-source"])
        {
            var benchmark = new JsonLegacySourceBenchmarks { RowCount = 100_000 };
            benchmark.Setup();
            return RunProfile("json-source", benchmark.NativeDataSource);
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
