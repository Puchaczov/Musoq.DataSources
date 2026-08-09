#nullable enable

using Musoq.DataSources.Structured;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues;

internal static class SeparatedValuesParallelScanOptions
{
    public const string MaximumParallelismSettingName = "separatedvalues.max_parallelism";
    public const int AutomaticMaximumParallelism = 2;
    public const long AutomaticCrossoverBytes = 1_500_000L;
    public const long PredicateCrossoverBytes = 12_000_000L;

    public static int Resolve(StructuredSchemaSnapshot snapshot, SourceExecutionContext executionContext)
    {
        var slicingAccepted = executionContext.Plan.AcceptedSkip.HasValue ||
                              executionContext.Plan.AcceptedTake.HasValue;
        var crossoverBytes = executionContext.Plan.AcceptedPredicate is null
            ? AutomaticCrossoverBytes
            : PredicateCrossoverBytes;
        var automaticEligible = !slicingAccepted &&
                                snapshot.Identity.Length >= crossoverBytes &&
                                snapshot.Partitions.Length > 1;

        var maximum = StructuredParallelism.ResolveMaximum(
            executionContext.SourceRuntimeSettings,
            MaximumParallelismSettingName,
            AutomaticMaximumParallelism,
            snapshot.Partitions.Length,
            automaticEligible);
        return slicingAccepted ? 1 : maximum;
    }
}
