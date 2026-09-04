using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Musoq.DataSources.SeparatedValues.Benchmark.Performance;

namespace Musoq.DataSources.SeparatedValues.Benchmark;

internal static class Program
{
    public static int Main(string[] args)
    {
        if (args is ["gate-query-rows", .. var queryRowGateArgs])
            return QueryRowQualificationGateCommand.Run(queryRowGateArgs, Console.Out, Console.Error);

        if (args is ["gate-enums"])
            return SeparatedValuesEnumQualificationGateCommand.Run(Console.Out, Console.Error);

        if (args is ["jit-query-row"])
            return QueryRowJitProbe.Run(Console.Out);

        if (args is ["inspect-query-row", var scenarioText, var transfer] &&
            Enum.TryParse<SeparatedValuesQueryRowCompiledScenario>(scenarioText, true, out var scenario) &&
            transfer is "legacy" or "query")
        {
            Console.WriteLine(
                SeparatedValuesQueryScopedCompiledExecutionBenchmarks.InspectGeneratedCode(
                    scenario,
                    transfer == "query"));
            return 0;
        }

        if (args is ["profile-query-row", var fieldCountText] &&
            int.TryParse(fieldCountText, out var fieldCount) &&
            fieldCount is 2 or 8 or 32 or 64)
        {
            var benchmark = new SeparatedValuesQueryScopedSourceMaterializationBenchmarks
            {
                FieldCount = fieldCount
            };
            benchmark.Setup();
            try
            {
                return RunProfile(
                    $"separated-values-query-row-{fieldCount}",
                    benchmark.QueryScopedNumericStructRows);
            }
            finally
            {
                benchmark.Cleanup();
            }
        }

        if (SeparatedValuesBenchmarkDataCommand.TryRun(args, out var exitCode))
            return exitCode;

        if (args is ["profile-source"])
        {
            var benchmark = new SeparatedValuesLegacySourceBenchmarks { RowCount = 100_000 };
            benchmark.Setup();
            return RunProfile("separated-values-source", benchmark.LegacyDataSource);
        }

        if (args is ["profile-compiled"])
        {
            var benchmark = new SeparatedValuesCompiledExecutionBenchmarks
            {
                RowCount = 100_000,
                QueryShape = SeparatedValuesQueryShape.FullRow
            };
            benchmark.Setup();
            return RunProfile("separated-values-compiled-full-row", benchmark.RunCompiledQuery);
        }

        if (args is ["smoke"])
        {
            foreach (var shape in Enum.GetValues<SeparatedValuesQueryShape>())
            {
                var benchmark = new SeparatedValuesCompiledExecutionBenchmarks
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
            Console.WriteLine(SeparatedValuesBenchmarkData.VerifyFixtures(10_000));
            return 0;
        }

        if (args is ["bakeoff-verify"])
            return SeparatedValuesParserBakeoffVerification.Run();

        if (args is ["query-row-smoke"])
            return RunQueryRowSmoke();

        if (args is ["scheduling-smoke", var rowText] &&
            int.TryParse(rowText, out var schedulingRows) &&
            schedulingRows > 0)
            return SeparatedValuesSchedulingMatrixRunner.Run(schedulingRows);

        if (args is ["scheduling-case", var caseRowsText, var caseShape, var blockText, var depthText, var yieldText] &&
            int.TryParse(caseRowsText, out var caseRows) &&
            int.TryParse(blockText, out var blockSizeMiB) &&
            int.TryParse(depthText, out var ioDepth) &&
            bool.TryParse(yieldText, out var yieldBeforeCpuWork) &&
            caseRows > 0)
        {
            return SeparatedValuesSchedulingMatrixRunner.RunCase(
                caseRows,
                caseShape,
                blockSizeMiB,
                ioDepth,
                yieldBeforeCpuWork);
        }

        if (args is ["profile-nvme", var sizeText, var workerText] &&
            int.TryParse(sizeText, out var sizeGiB) &&
            int.TryParse(workerText, out var workers) &&
            sizeGiB > 0 &&
            workers >= 0)
        {
            var benchmark = new SeparatedValuesNvmePipelineBenchmarks
            {
                SizeGiB = sizeGiB,
                WorkerCount = workers
            };
            benchmark.Setup();
            var rawNvme = RunMeasured(
                "raw-nvme-multi-read",
                sizeGiB,
                Math.Clamp(Environment.ProcessorCount / 2, 4, 16),
                benchmark.RawNvmeMultiRead);
            var zeroColumn = RunMeasured("zero-column-pipeline", sizeGiB, workers == 0
                ? Math.Min(4, Math.Max(1, Environment.ProcessorCount - 1))
                : Math.Min(4, workers), benchmark.ZeroColumnPipeline);
            RunMeasured("raw-sequential", sizeGiB, 1, benchmark.RawSequentialRead);
            RunMeasured("raw-buffered-multi-read", sizeGiB, Math.Clamp(Environment.ProcessorCount / 2, 4, 16), benchmark.RawMultiRead);
            RunMeasured("zero-column-pipeline-warm", sizeGiB, workers == 0
                ? Math.Min(4, Math.Max(1, Environment.ProcessorCount - 1))
                : Math.Min(4, workers), benchmark.ZeroColumnPipeline);
            var predicate = RunMeasured("rejected-numeric-predicate", sizeGiB, workers == 0
                ? Math.Max(1, Environment.ProcessorCount - 1)
                : workers, benchmark.RejectedNumericPredicatePipeline);
            var ceilingRatio = zeroColumn.ThroughputMiBPerSecond / rawNvme.ThroughputMiBPerSecond * 100d;
            Console.WriteLine(
                $"qualification: framing-vs-nvme={ceilingRatio:F1}% " +
                $"({(ceilingRatio >= 85d ? "PASS" : "FAIL")}); " +
                $"cpu-worker-utilization={predicate.GrantedUtilization:F1}% " +
                $"({(predicate.GrantedUtilization >= 80d ? "PASS" : "FAIL")})");
            return 0;
        }

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        return 0;
    }

    private static int RunQueryRowSmoke()
    {
        var sourceMethods = typeof(SeparatedValuesQueryScopedSourceMaterializationBenchmarks)
            .GetMethods()
            .Where(static method => method.GetCustomAttributes(typeof(BenchmarkAttribute), inherit: false).Length != 0)
            .OrderBy(static method => method.Name, StringComparer.Ordinal)
            .ToArray();
        var compiledMethods = typeof(SeparatedValuesQueryScopedCompiledExecutionBenchmarks)
            .GetMethods()
            .Where(static method => method.GetCustomAttributes(typeof(BenchmarkAttribute), inherit: false).Length != 0)
            .OrderBy(static method => method.Name, StringComparer.Ordinal)
            .ToArray();
        if (sourceMethods.Length != 24 || compiledMethods.Length != 4)
        {
            throw new InvalidOperationException(
                $"Query-row cohort shape is incomplete: source={sourceMethods.Length}/24, compiled={compiledMethods.Length}/4.");
        }

        long checksum = 0;
        var sourceIdentities = 0;
        foreach (var fieldCount in new[] { 2, 8, 32, 64 })
        {
            var benchmark = new SeparatedValuesQueryScopedSourceMaterializationBenchmarks
            {
                FieldCount = fieldCount
            };
            benchmark.Setup();
            try
            {
                foreach (var method in sourceMethods)
                {
                    checksum = unchecked(checksum * 31 + Convert.ToInt64(method.Invoke(benchmark, null)));
                    sourceIdentities++;
                }
            }
            finally
            {
                benchmark.Cleanup();
            }
        }

        var compiledIdentities = 0;
        foreach (var scenario in Enum.GetValues<SeparatedValuesQueryRowCompiledScenario>())
        {
            var benchmark = new SeparatedValuesQueryScopedCompiledExecutionBenchmarks
            {
                Scenario = scenario
            };
            benchmark.Setup();
            try
            {
                foreach (var method in compiledMethods)
                {
                    checksum = unchecked(checksum * 31 + Convert.ToInt64(method.Invoke(benchmark, null)));
                    compiledIdentities++;
                }
            }
            finally
            {
                benchmark.Cleanup();
            }
        }

        Console.WriteLine(
            $"query-row-smoke: source={sourceIdentities}/96, compiled={compiledIdentities}/36, checksum={checksum}");
        return sourceIdentities == 96 && compiledIdentities == 36 ? 0 : 1;
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

    private static Measurement RunMeasured(
        string name,
        int sizeGiB,
        int grantedWorkers,
        Func<long> operation)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var process = Process.GetCurrentProcess();
        var cpuBefore = process.TotalProcessorTime;
        var allocatedBefore = GC.GetTotalAllocatedBytes(true);
        var stopwatch = Stopwatch.StartNew();
        var checksum = operation();
        stopwatch.Stop();
        var cpu = process.TotalProcessorTime - cpuBefore;
        var allocated = GC.GetTotalAllocatedBytes(false) - allocatedBefore;
        var throughput = sizeGiB * 1024d / stopwatch.Elapsed.TotalSeconds;
        var activeWorkers = cpu.TotalSeconds / stopwatch.Elapsed.TotalSeconds;
        var utilization = grantedWorkers == 0 ? 0 : activeWorkers / grantedWorkers * 100d;
        Console.WriteLine(
            $"{name}: elapsed={stopwatch.Elapsed.TotalSeconds:F3}s, throughput={throughput:F1} MiB/s, " +
            $"cpu={cpu.TotalSeconds:F3}s, activeWorkers={activeWorkers:F2}, " +
            $"grantedUtilization={utilization:F1}%, allocated={allocated / 1024d / 1024d:F1} MiB, checksum={checksum}");
        return new Measurement(throughput, utilization);
    }

    private readonly record struct Measurement(
        double ThroughputMiBPerSecond,
        double GrantedUtilization);
}
