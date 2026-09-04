using System.Diagnostics;
using System.Globalization;
using System.Text;
using Musoq.Schema;

namespace Musoq.DataSources.SeparatedValues.Benchmark.Performance;

internal static class SeparatedValuesEnumQualificationGateCommand
{
    private const int RecordCount = 8_192;
    private const int Reports = 3;
    private const int IterationsPerReport = 256;

    public static int Run(TextWriter output, TextWriter error)
    {
        try
        {
            var result = SeparatedValuesEnumQualificationGate.Run();
            foreach (var report in result.Reports)
            {
                output.WriteLine(
                    $"enum-report-{report.Index}: records={RecordCount:N0}, " +
                    $"numeric={report.NumericRatio.ToString("F4", CultureInfo.InvariantCulture)}x/" +
                    $"{report.NumericAllocation:N0} B, " +
                    $"symbolic={report.SymbolicRatio.ToString("F4", CultureInfo.InvariantCulture)}x/" +
                    $"{report.SymbolicAllocation:N0} B, " +
                    $"flags={report.FlagsRatio.ToString("F4", CultureInfo.InvariantCulture)}x/" +
                    $"{report.FlagsAllocation:N0} B");
            }

            output.WriteLine(
                $"enum-median: numeric={result.NumericMedianRatio.ToString("F4", CultureInfo.InvariantCulture)}x, " +
                $"symbolic={result.SymbolicMedianRatio.ToString("F4", CultureInfo.InvariantCulture)}x, " +
                $"flags={result.FlagsMedianRatio.ToString("F4", CultureInfo.InvariantCulture)}x, " +
                $"max-allocation={result.MaximumAllocation:N0} B");
            output.WriteLine(
                "gate-enums: " + (result.Passed ? "PASS" : "FAIL") +
                " (MediumRun, 8,192 records, three isolated reports)");
            return result.Passed ? 0 : 1;
        }
        catch (Exception exception) when (exception is InvalidOperationException or FormatException)
        {
            error.WriteLine($"gate-enums: FAIL ({exception.Message})");
            return 1;
        }
    }
}

internal sealed record SeparatedValuesEnumQualificationResult(
    IReadOnlyList<SeparatedValuesEnumQualificationReport> Reports,
    double NumericMedianRatio,
    double SymbolicMedianRatio,
    double FlagsMedianRatio,
    long MaximumAllocation)
{
    public bool Passed => NumericMedianRatio <= 1.03d &&
                          SymbolicMedianRatio <= 1.10d &&
                          FlagsMedianRatio <= 1.02d &&
                          MaximumAllocation <= 1_024L;
}

internal sealed record SeparatedValuesEnumQualificationReport(
    int Index,
    double NumericRatio,
    double SymbolicRatio,
    double FlagsRatio,
    long NumericAllocation,
    long SymbolicAllocation,
    long FlagsAllocation);

internal static class SeparatedValuesEnumQualificationGate
{
    private const int RecordCount = 8_192;
    private const int Reports = 3;
    private const int IterationsPerReport = 256;
    private const int MeasurementRepetitions = 8;

    private static readonly byte[][] NumericInputs =
    [
        Encoding.UTF8.GetBytes("20"),
        Encoding.UTF8.GetBytes("21"),
        Encoding.UTF8.GetBytes("22"),
        Encoding.UTF8.GetBytes("23")
    ];
    private static readonly byte[][] SymbolicInputs =
    [
        Encoding.UTF8.GetBytes("Running"),
        Encoding.UTF8.GetBytes("Queued")
    ];
    private static readonly byte[][] FlagsInputs =
    [
        Encoding.UTF8.GetBytes("3"),
        Encoding.UTF8.GetBytes("1")
    ];
    private static readonly SeparatedValuesEnumPlan StatusPlan = CreateStatusPlan();
    private static readonly SeparatedValuesEnumPlan FlagsPlan = CreateFlagsPlan();
    private static int InputSalt;

    public static SeparatedValuesEnumQualificationResult Run()
    {
        InputSalt = Environment.TickCount;
        // Warm both production and comparator paths before allocation/timing samples.
        _ = RunNumericProduction(1);
        _ = RunNumericPrimitive(1);
        _ = RunSymbolicProduction(1);
        _ = RunSymbolicComparator(1);
        _ = RunFlagsProduction(1);
        _ = RunFlagsPrimitive(1);

        var reports = new List<SeparatedValuesEnumQualificationReport>(Reports);
        for (var index = 1; index <= Reports; index++)
        {
            var numeric = Measure(RunNumericProduction, RunNumericPrimitive);
            var symbolic = Measure(RunSymbolicProduction, RunSymbolicComparator);
            var flags = Measure(RunFlagsProduction, RunFlagsPrimitive);
            reports.Add(new SeparatedValuesEnumQualificationReport(
                index,
                numeric.Ratio,
                symbolic.Ratio,
                flags.Ratio,
                numeric.Allocation,
                symbolic.Allocation,
                flags.Allocation));
        }

        return new SeparatedValuesEnumQualificationResult(
            reports.AsReadOnly(),
            Median(reports.Select(static report => report.NumericRatio)),
            Median(reports.Select(static report => report.SymbolicRatio)),
            Median(reports.Select(static report => report.FlagsRatio)),
            reports.SelectMany(static report => new[]
                {
                    report.NumericAllocation,
                    report.SymbolicAllocation,
                    report.FlagsAllocation
                })
                .Max());
    }

    private static SeparatedValuesEnumPlan CreateStatusPlan()
    {
        var descriptor = new EnumTypeDescriptor(
            "Status",
            EnumTypeOrigin.QueryLocal,
            EnumUnderlyingKind.Int32,
            false,
            [
                new EnumMemberDescriptor("Queued", EnumScalarValue.FromInt32(10)),
                new EnumMemberDescriptor("Running", EnumScalarValue.FromInt32(20))
            ]);
        return SeparatedValuesEnumPlan.Create(0, typeof(int), descriptor);
    }

    private static SeparatedValuesEnumPlan CreateFlagsPlan()
    {
        var descriptor = new EnumTypeDescriptor(
            "Access",
            EnumTypeOrigin.QueryLocal,
            EnumUnderlyingKind.UInt32,
            true,
            [
                new EnumMemberDescriptor("None", EnumScalarValue.FromUInt32(0)),
                new EnumMemberDescriptor("Read", EnumScalarValue.FromUInt32(1)),
                new EnumMemberDescriptor("Write", EnumScalarValue.FromUInt32(2)),
                new EnumMemberDescriptor("ReadWrite", EnumScalarValue.FromUInt32(3))
            ]);
        return SeparatedValuesEnumPlan.Create(0, typeof(uint), descriptor);
    }

    private static long RunNumericProduction(int multiplier)
    {
        long checksum = 0;
        for (var iteration = 0; iteration < multiplier * IterationsPerReport; iteration++)
        {
            for (var row = 0; row < RecordCount; row++)
            {
                var input = NumericInputs[(row + InputSalt) & 3];
                var field = new SeparatedValuesUtf8Field(input, 0, false, false);
                if (!StatusPlan.TryDecode(field, out var parsed))
                    throw new FormatException("The numeric enum benchmark field did not decode.");
                checksum += parsed.Int32;
            }
        }

        return checksum;
    }

    private static long RunNumericPrimitive(int multiplier)
    {
        long checksum = 0;
        for (var iteration = 0; iteration < multiplier * IterationsPerReport; iteration++)
        {
            for (var row = 0; row < RecordCount; row++)
            {
                var input = NumericInputs[(row + InputSalt) & 3];
                var field = new SeparatedValuesUtf8Field(input, 0, false, false);
                if (!SeparatedValuesParsedValue.TryParse(
                        field,
                        SeparatedValuesConversion.Int32,
                        CultureInfo.InvariantCulture,
                        out var parsed))
                    throw new FormatException("The numeric primitive benchmark field did not decode.");
                checksum += EnumScalarValue.FromInt32(parsed.Int32).AsInt32();
            }
        }

        return checksum;
    }

    private static long RunSymbolicProduction(int multiplier)
    {
        long checksum = 0;
        for (var iteration = 0; iteration < multiplier * IterationsPerReport; iteration++)
        {
            for (var row = 0; row < RecordCount; row++)
            {
                var input = SymbolicInputs[(row + InputSalt) & 1];
                var field = new SeparatedValuesUtf8Field(input, 0, false, false);
                if (!StatusPlan.TryDecode(field, out var parsed))
                    throw new FormatException("The symbolic enum benchmark field did not decode.");
                checksum += parsed.Int32;
            }
        }

        return checksum;
    }

    private static long RunSymbolicComparator(int multiplier)
    {
        long checksum = 0;
        for (var iteration = 0; iteration < multiplier * IterationsPerReport; iteration++)
        {
            for (var row = 0; row < RecordCount; row++)
            {
                var input = SymbolicInputs[(row + InputSalt) & 1];
                var field = new SeparatedValuesUtf8Field(input, 0, false, false);
                // Hash plus exact UTF-8 verification is the optimized allocation-free
                // token comparator against which the descriptor lookup is qualified.
                var hash = SeparatedValuesEnumPlan.HashUtf8(input);
                var names = StatusPlan.Names;
                var first = 0;
                var last = names.Length;
                while (first < last)
                {
                    var middle = first + ((last - first) >> 1);
                    if (names[middle].Hash < hash)
                        first = middle + 1;
                    else
                        last = middle;
                }

                for (var index = first; index < names.Length && names[index].Hash == hash; index++)
                {
                    if (field.ValueEquals(names[index].Utf8Name))
                        checksum += SeparatedValuesParsedValue.FromEnum(
                            SeparatedValuesConversion.Int32,
                            names[index].Value).Int32;
                }
            }
        }

        return checksum;
    }

    private static long RunFlagsProduction(int multiplier)
    {
        long checksum = 0;
        for (var iteration = 0; iteration < multiplier * IterationsPerReport; iteration++)
        {
            for (var row = 0; row < RecordCount; row++)
            {
                var input = FlagsInputs[(row + InputSalt) & 1];
                var field = new SeparatedValuesUtf8Field(input, 0, false, false);
                if (!FlagsPlan.TryDecode(field, out var parsed))
                    throw new FormatException("The flags enum benchmark field did not decode.");
                checksum += (parsed.UInt32 & 3u) == 3u ? 1 : 0;
            }
        }

        return checksum;
    }

    private static long RunFlagsPrimitive(int multiplier)
    {
        long checksum = 0;
        for (var iteration = 0; iteration < multiplier * IterationsPerReport; iteration++)
        {
            for (var row = 0; row < RecordCount; row++)
            {
                var input = FlagsInputs[(row + InputSalt) & 1];
                var field = new SeparatedValuesUtf8Field(input, 0, false, false);
                if (!SeparatedValuesParsedValue.TryParse(
                        field,
                        SeparatedValuesConversion.UInt32,
                        CultureInfo.InvariantCulture,
                        out var parsed))
                    throw new FormatException("The flags primitive benchmark field did not decode.");
                var value = SeparatedValuesParsedValue.FromEnum(
                    SeparatedValuesConversion.UInt32,
                    EnumScalarValue.FromUInt32(parsed.UInt32)).UInt32;
                checksum += (value & 3u) == 3u ? 1 : 0;
            }
        }

        return checksum;
    }

    private static EnumMeasurement Measure(Func<int, long> production, Func<int, long> comparator)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var beforeAllocation = GC.GetAllocatedBytesForCurrentThread();
        var productionTime = Stopwatch.GetTimestamp();
        long productionChecksum = 0;
        for (var iteration = 0; iteration < MeasurementRepetitions; iteration++)
            productionChecksum += production(1);
        var productionTicks = Stopwatch.GetTimestamp() - productionTime;
        var productionAllocation = GC.GetAllocatedBytesForCurrentThread() - beforeAllocation;

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        beforeAllocation = GC.GetAllocatedBytesForCurrentThread();
        var comparatorTime = Stopwatch.GetTimestamp();
        long comparatorChecksum = 0;
        for (var iteration = 0; iteration < MeasurementRepetitions; iteration++)
            comparatorChecksum += comparator(1);
        var comparatorTicks = Stopwatch.GetTimestamp() - comparatorTime;
        var comparatorAllocation = GC.GetAllocatedBytesForCurrentThread() - beforeAllocation;

        if (productionChecksum == 0 || comparatorChecksum == 0)
            throw new InvalidOperationException("Enum benchmark checksum was unexpectedly zero.");

        var productionNanoseconds = productionTicks * 1_000_000_000d / Stopwatch.Frequency;
        var comparatorNanoseconds = comparatorTicks * 1_000_000_000d / Stopwatch.Frequency;
        return new EnumMeasurement(
            productionNanoseconds / comparatorNanoseconds,
            Math.Max(productionAllocation, comparatorAllocation));
    }

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(static value => value).ToArray();
        return sorted[sorted.Length / 2];
    }

    private readonly record struct EnumMeasurement(double Ratio, long Allocation);
}
