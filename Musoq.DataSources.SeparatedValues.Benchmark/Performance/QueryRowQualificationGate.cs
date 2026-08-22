using System.Globalization;

namespace Musoq.DataSources.SeparatedValues.Benchmark.Performance;

internal sealed record QueryRowQualificationInputs(
    IReadOnlyList<string> SourceReports,
    IReadOnlyList<string> CompiledReports,
    IReadOnlyList<string> CoreSourceReports,
    string DisassemblyPath);

internal sealed record QueryRowQualificationCheck(string Name, bool Passed, string Detail);

internal sealed record QueryRowQualificationResult(IReadOnlyList<QueryRowQualificationCheck> Checks)
{
    public bool IsSuccess => Checks.All(static check => check.Passed);
}
internal static class QueryRowQualificationGate
{
    public const int MinimumSamples = 3;
    private const int MaterializedRows = 2048;
    private const string SourceBenchmark = nameof(SeparatedValuesQueryScopedSourceMaterializationBenchmarks);
    private const string CompiledBenchmark = nameof(SeparatedValuesQueryScopedCompiledExecutionBenchmarks);
    private const string CoreSourceBenchmark = "QueryScopedSourceMaterializationBenchmark";

    private static readonly int[] FieldCounts = [2, 8, 32, 64];
    private static readonly string[][] SourceMethodGroups =
    [
        [nameof(SeparatedValuesQueryScopedSourceMaterializationBenchmarks.LegacyRows), nameof(SeparatedValuesQueryScopedSourceMaterializationBenchmarks.QueryScopedStructRows), nameof(SeparatedValuesQueryScopedSourceMaterializationBenchmarks.QueryScopedClassRows)],
        [nameof(SeparatedValuesQueryScopedSourceMaterializationBenchmarks.LegacySelectiveProjection), nameof(SeparatedValuesQueryScopedSourceMaterializationBenchmarks.QueryScopedSelectiveProjection), nameof(SeparatedValuesQueryScopedSourceMaterializationBenchmarks.QueryScopedClassSelectiveProjection)],
        [nameof(SeparatedValuesQueryScopedSourceMaterializationBenchmarks.LegacyHighRejection), nameof(SeparatedValuesQueryScopedSourceMaterializationBenchmarks.QueryScopedHighRejection), nameof(SeparatedValuesQueryScopedSourceMaterializationBenchmarks.QueryScopedClassHighRejection)],
        [nameof(SeparatedValuesQueryScopedSourceMaterializationBenchmarks.LegacyAggregation), nameof(SeparatedValuesQueryScopedSourceMaterializationBenchmarks.QueryScopedStructAggregation), nameof(SeparatedValuesQueryScopedSourceMaterializationBenchmarks.QueryScopedClassAggregation)],
        [nameof(SeparatedValuesQueryScopedSourceMaterializationBenchmarks.LegacyEarlyTake), nameof(SeparatedValuesQueryScopedSourceMaterializationBenchmarks.QueryScopedEarlyTake), nameof(SeparatedValuesQueryScopedSourceMaterializationBenchmarks.QueryScopedClassEarlyTake)],
        [nameof(SeparatedValuesQueryScopedSourceMaterializationBenchmarks.LegacyNumericRows), nameof(SeparatedValuesQueryScopedSourceMaterializationBenchmarks.QueryScopedNumericStructRows), nameof(SeparatedValuesQueryScopedSourceMaterializationBenchmarks.QueryScopedNumericClassRows)],
        [nameof(SeparatedValuesQueryScopedSourceMaterializationBenchmarks.LegacyObjectArrayMaterialization), nameof(SeparatedValuesQueryScopedSourceMaterializationBenchmarks.QueryScopedStructMaterialization), nameof(SeparatedValuesQueryScopedSourceMaterializationBenchmarks.QueryScopedClassMaterialization)],
        [nameof(SeparatedValuesQueryScopedSourceMaterializationBenchmarks.LegacyNumericObjectArrayMaterialization), nameof(SeparatedValuesQueryScopedSourceMaterializationBenchmarks.QueryScopedNumericStructMaterialization), nameof(SeparatedValuesQueryScopedSourceMaterializationBenchmarks.QueryScopedNumericClassMaterialization)]
    ];

    private static readonly string[] CompiledMethods =
    [
        nameof(SeparatedValuesQueryScopedCompiledExecutionBenchmarks.LegacyWarmExecution),
        nameof(SeparatedValuesQueryScopedCompiledExecutionBenchmarks.QueryScopedWarmExecution),
        nameof(SeparatedValuesQueryScopedCompiledExecutionBenchmarks.LegacyColdCompileAndFirstRun),
        nameof(SeparatedValuesQueryScopedCompiledExecutionBenchmarks.QueryScopedColdCompileAndFirstRun)
    ];

    public static QueryRowQualificationResult Evaluate(QueryRowQualificationInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ValidateReports(nameof(inputs.SourceReports), inputs.SourceReports);
        ValidateReports(nameof(inputs.CompiledReports), inputs.CompiledReports);
        ValidateReports(nameof(inputs.CoreSourceReports), inputs.CoreSourceReports);

        var sourceReports = ReadCohort("source", inputs.SourceReports);
        var compiledReports = ReadCohort("compiled", inputs.CompiledReports);
        var coreSourceReports = ReadCohort("core source", inputs.CoreSourceReports);
        ValidateEnvironment(sourceReports, compiledReports, coreSourceReports);
        ValidateMatchingJobs("source/core source", sourceReports.Concat(coreSourceReports));
        ValidateMatrix(sourceReports, compiledReports, coreSourceReports);

        var checks = new List<QueryRowQualificationCheck>();
        foreach (var fieldCount in FieldCounts)
        {
            var legacyCarrier = Median(
                sourceReports,
                SourceName(nameof(SeparatedValuesQueryScopedSourceMaterializationBenchmarks.LegacyNumericObjectArrayMaterialization), fieldCount));
            var structCarrier = Median(
                sourceReports,
                SourceName(nameof(SeparatedValuesQueryScopedSourceMaterializationBenchmarks.QueryScopedNumericStructMaterialization), fieldCount));
            var classCarrier = Median(
                sourceReports,
                SourceName(nameof(SeparatedValuesQueryScopedSourceMaterializationBenchmarks.QueryScopedNumericClassMaterialization), fieldCount));
            var throughput = Ratio(legacyCarrier.MeanNanoseconds, structCarrier.MeanNanoseconds);
            var allocationReduction = Reduction(legacyCarrier.AllocatedBytes, structCarrier.AllocatedBytes);
            var maximumClassBytes = MaterializedRows * AlignToEight(24 + fieldCount * sizeof(int));

            checks.Add(Check(
                $"carrier-throughput-{fieldCount}",
                throughput >= 2d,
                $"{Format(throughput)}x legacy throughput; required >= 2.0000x"));
            checks.Add(Check(
                $"carrier-allocation-reduction-{fieldCount}",
                allocationReduction >= 0.9d,
                $"{FormatPercent(allocationReduction)} reduction; required >= 90.00%"));
            checks.Add(Check(
                $"struct-carrier-allocation-{fieldCount}",
                structCarrier.AllocatedBytes == 0d,
                $"{FormatBytes(structCarrier.AllocatedBytes)} allocated; required 0 B"));
            checks.Add(Check(
                $"class-carrier-allocation-{fieldCount}",
                classCarrier.AllocatedBytes <= maximumClassBytes,
                $"{FormatBytes(classCarrier.AllocatedBytes)} allocated; one-carrier ceiling {maximumClassBytes} B"));

            var legacyCsv = Median(
                sourceReports,
                SourceName(nameof(SeparatedValuesQueryScopedSourceMaterializationBenchmarks.LegacyNumericRows), fieldCount));
            var structCsv = Median(
                sourceReports,
                SourceName(nameof(SeparatedValuesQueryScopedSourceMaterializationBenchmarks.QueryScopedNumericStructRows), fieldCount));
            var csvReduction = Reduction(legacyCsv.AllocatedBytes, structCsv.AllocatedBytes);
            checks.Add(Check(
                $"numeric-csv-allocation-{fieldCount}",
                csvReduction >= 0.2d,
                $"{FormatPercent(csvReduction)} reduction; required >= 20.00%"));

            var coreStructCsv = Median(
                coreSourceReports,
                CoreSourceName("QueryScopedNumericStructRows", fieldCount));
            var coreTimeRatio = Ratio(structCsv.MeanNanoseconds, coreStructCsv.MeanNanoseconds);
            var coreAllocationRatio = Ratio(structCsv.AllocatedBytes, coreStructCsv.AllocatedBytes);
            checks.Add(Check(
                $"core-query-time-{fieldCount}",
                coreTimeRatio <= 0.8d,
                $"{Format(coreTimeRatio)}x core query time; required <= 0.8000x"));
            checks.Add(Check(
                $"core-query-allocation-{fieldCount}",
                coreAllocationRatio <= 0.8d,
                $"{Format(coreAllocationRatio)}x core query allocation; required <= 0.8000x"));
        }

        foreach (var scenario in Enum.GetValues<SeparatedValuesQueryRowCompiledScenario>())
        {
            var legacy = Median(
                compiledReports,
                CompiledName(nameof(SeparatedValuesQueryScopedCompiledExecutionBenchmarks.LegacyWarmExecution), scenario));
            var queryScoped = Median(
                compiledReports,
                CompiledName(nameof(SeparatedValuesQueryScopedCompiledExecutionBenchmarks.QueryScopedWarmExecution), scenario));
            var ratio = Ratio(queryScoped.MeanNanoseconds, legacy.MeanNanoseconds);
            var maximum = scenario is SeparatedValuesQueryRowCompiledScenario.NullableString8Full or
                SeparatedValuesQueryRowCompiledScenario.NullableString8HighRejection
                ? 1.05d
                : 1.03d;
            checks.Add(Check(
                $"warm-regression-{scenario}",
                ratio <= maximum,
                $"{Format(ratio)}x legacy time; required <= {Format(maximum)}x"));
        }

        checks.AddRange(EvaluateDisassembly(inputs.DisassemblyPath));
        return new QueryRowQualificationResult(checks.AsReadOnly());
    }

    private static IReadOnlyList<QueryRowQualificationCheck> EvaluateDisassembly(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("A query-row disassembly path is required.", nameof(path));
        if (!File.Exists(path))
            throw new FileNotFoundException("Query-row disassembly was not found.", path);

        var lines = File.ReadAllLines(path);
        var fieldReader = FindDisassemblyRegion(
            lines,
            static line =>
                line.Contains("Assembly listing for method", StringComparison.OrdinalIgnoreCase) &&
                line.Contains("SeparatedValuesFieldReader", StringComparison.Ordinal) &&
                line.Contains("BenchmarkNullableNumericStructRow8", StringComparison.Ordinal) &&
                line.Contains(":Read[System.Nullable`1[int]]", StringComparison.Ordinal),
            "the warmed production 8-field nullable numeric field reader");
        var typedRead = FindDisassemblyRegion(
            lines,
            static line =>
                line.Contains("Assembly listing for method", StringComparison.OrdinalIgnoreCase) &&
                line.Contains("SeparatedValuesTypedValueReader:Read[System.Nullable`1[int]]", StringComparison.Ordinal),
            "the warmed production nullable Int32 typed conversion");

        return
        [
            EvaluateDisassemblyRegion("production-field-reader-disassembly", fieldReader),
            EvaluateDisassemblyRegion("production-typed-read-disassembly", typedRead)
        ];
    }

    private static string[] FindDisassemblyRegion(
        string[] lines,
        Predicate<string> isTarget,
        string description)
    {
        var start = Array.FindIndex(lines, isTarget);
        if (start < 0)
            throw new InvalidDataException($"Query-row disassembly does not contain {description}.");

        var end = Array.FindIndex(
            lines,
            start + 1,
            static line => line.Contains("Assembly listing for method", StringComparison.OrdinalIgnoreCase));
        return lines[start..(end < 0 ? lines.Length : end)];
    }

    private static QueryRowQualificationCheck EvaluateDisassemblyRegion(string name, string[] lines)
    {
        var forbiddenMarkers = new[]
        {
            "Nullable:GetUnderlyingType",
            "GetUnderlyingType(",
            "RuntimeType:GetGenericArguments",
            "GetGenericArguments(",
            "System.Type[]",
            "object[]",
            "CORINFO_HELP_NEWARR",
            "CORINFO_HELP_BOX",
            "System.Reflection",
            "System.Linq.Expressions",
            "CreateDelegate",
            "callvirt",
            "interface dispatch",
            "VIRTUAL_FUNC_PTR"
        };
        var forbidden = lines
            .Where(line =>
                forbiddenMarkers.Any(marker => line.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        return Check(
            name,
            forbidden.Length == 0,
            forbidden.Length == 0
                ? "no nullable reflection, array allocation, boxing, delegates, or interface/virtual dispatch markers"
                : $"forbidden markers: {string.Join(" | ", forbidden.Select(static line => line.Trim()))}");
    }

    private static void ValidateMatrix(
        IReadOnlyList<BenchmarkReportData> sourceReports,
        IReadOnlyList<BenchmarkReportData> compiledReports,
        IReadOnlyList<BenchmarkReportData> coreSourceReports)
    {
        foreach (var fieldCount in FieldCounts)
        {
            foreach (var group in SourceMethodGroups)
            {
                foreach (var method in group)
                    Require(sourceReports, SourceName(method, fieldCount));
            }
        }

        foreach (var scenario in Enum.GetValues<SeparatedValuesQueryRowCompiledScenario>())
        {
            foreach (var method in CompiledMethods)
                Require(compiledReports, CompiledName(method, scenario));
        }

        foreach (var fieldCount in FieldCounts)
            Require(coreSourceReports, CoreSourceName("QueryScopedNumericStructRows", fieldCount));
    }

    private static BenchmarkReportData[] ReadCohort(
        string name,
        IReadOnlyList<string> paths)
    {
        var reports = paths.Select(BenchmarkReportReader.ReadReport).ToArray();
        var expected = reports[0].Metrics.Keys.ToHashSet(StringComparer.Ordinal);
        var expectedJobs = reports[0].JobFingerprints;
        for (var index = 1; index < reports.Length; index++)
        {
            var missing = expected.Except(reports[index].Metrics.Keys, StringComparer.Ordinal).Order().ToArray();
            var extra = reports[index].Metrics.Keys.Except(expected, StringComparer.Ordinal).Order().ToArray();
            if (missing.Length == 0 && extra.Length == 0)
            {
                if (!reports[index].JobFingerprints.SequenceEqual(expectedJobs, StringComparer.Ordinal))
                {
                    throw new InvalidDataException(
                        $"The query-row {name} reports have different jobs at sample {index + 1}.");
                }
                continue;
            }

            throw new InvalidDataException(
                $"The query-row {name} reports have different scenario sets at sample {index + 1}. " +
                $"Missing: {FormatNames(missing)}. Extra: {FormatNames(extra)}.");
        }

        return reports;
    }

    private static void Require(
        IReadOnlyList<BenchmarkReportData> reports,
        string name)
    {
        if (reports.All(report => report.Metrics.ContainsKey(name)))
            return;

        throw new InvalidDataException($"Query-row qualification report is missing scenario '{name}'.");
    }

    private static BenchmarkMetric Median(
        IReadOnlyList<BenchmarkReportData> reports,
        string name)
    {
        return new BenchmarkMetric(
            Median(reports.Select(report => report.Metrics[name].MeanNanoseconds)),
            Median(reports.Select(report => report.Metrics[name].AllocatedBytes)));
    }

    private static void ValidateEnvironment(params IReadOnlyList<BenchmarkReportData>[] cohorts)
    {
        var reports = cohorts.SelectMany(static cohort => cohort).ToArray();
        var expectedEnvironment = reports[0].Environment;
        for (var index = 1; index < reports.Length; index++)
        {
            if (reports[index].Environment != expectedEnvironment)
            {
                throw new InvalidDataException(
                    $"Query-row report environment mismatch at report {index + 1}: " +
                    $"expected '{expectedEnvironment}', observed '{reports[index].Environment}'.");
            }

        }
    }

    private static void ValidateMatchingJobs(string name, IEnumerable<BenchmarkReportData> reports)
    {
        var values = reports.ToArray();
        var expected = values[0].JobFingerprints;
        for (var index = 1; index < values.Length; index++)
        {
            if (values[index].JobFingerprints.SequenceEqual(expected, StringComparer.Ordinal))
                continue;
            throw new InvalidDataException(
                $"Query-row {name} job mismatch at report {index + 1}: expected " +
                $"'{string.Join(", ", expected)}', observed " +
                $"'{string.Join(", ", values[index].JobFingerprints)}'.");
        }
    }

    private static double Median(IEnumerable<double> source)
    {
        var values = source.Order().ToArray();
        var midpoint = values.Length / 2;
        return values.Length % 2 == 1
            ? values[midpoint]
            : (values[midpoint - 1] + values[midpoint]) / 2d;
    }

    private static void ValidateReports(string name, IReadOnlyList<string> reports)
    {
        ArgumentNullException.ThrowIfNull(reports);
        if (reports.Count < MinimumSamples)
        {
            throw new ArgumentException(
                $"At least {MinimumSamples} query-row benchmark reports are required.",
                name);
        }
    }

    private static string SourceName(string method, int fieldCount) =>
        $"Musoq.DataSources.SeparatedValues.Benchmark.{SourceBenchmark}.{method}(FieldCount: {fieldCount})";

    private static string CompiledName(string method, SeparatedValuesQueryRowCompiledScenario scenario) =>
        $"Musoq.DataSources.SeparatedValues.Benchmark.{CompiledBenchmark}.{method}(Scenario: {scenario})";

    private static string CoreSourceName(string method, int fieldCount) =>
        $"Musoq.Benchmarks.{CoreSourceBenchmark}.{method}(FieldCount: {fieldCount})";

    private static QueryRowQualificationCheck Check(string name, bool passed, string detail) =>
        new(name, passed, detail);

    private static int AlignToEight(int value) => (value + 7) & ~7;

    private static double Ratio(double value, double baseline) =>
        baseline == 0d ? value == 0d ? 1d : double.PositiveInfinity : value / baseline;

    private static double Reduction(double baseline, double value) =>
        baseline == 0d ? value == 0d ? 1d : double.NegativeInfinity : 1d - value / baseline;

    private static string Format(double value) => value.ToString("F4", CultureInfo.InvariantCulture);

    private static string FormatPercent(double value) => value.ToString("P2", CultureInfo.InvariantCulture);

    private static string FormatBytes(double value) => value.ToString("F0", CultureInfo.InvariantCulture) + " B";

    private static string FormatNames(IReadOnlyCollection<string> names) =>
        names.Count == 0 ? "none" : string.Join(", ", names);
}
