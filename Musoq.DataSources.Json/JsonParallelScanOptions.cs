#nullable enable

using Musoq.DataSources.Structured;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Json;

internal static class JsonParallelScanOptions
{
    public const string MaximumParallelismSettingName = "json.max_parallelism";
    public const int AutomaticMaximumParallelism = 4;
    public const long AutomaticCrossoverBytes = 3_000_000L;

    public static int Resolve(StructuredSchemaSnapshot snapshot, SourceExecutionContext executionContext)
    {
        var slicingAccepted = executionContext.Plan.AcceptedSkip.HasValue ||
                              executionContext.Plan.AcceptedTake.HasValue;
        var automaticEligible = !slicingAccepted &&
                                snapshot.Identity.Length >= AutomaticCrossoverBytes &&
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
