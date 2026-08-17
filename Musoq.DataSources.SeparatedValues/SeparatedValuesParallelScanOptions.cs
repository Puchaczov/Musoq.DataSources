#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Musoq.DataSources.Structured;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues;

internal static class SeparatedValuesParallelScanOptions
{
    public const string MaximumParallelismSettingName = "separatedvalues.max_parallelism";
    public const long AutomaticCrossoverBytes = 64L * 1024L * 1024L;

    public static int AutomaticMaximumParallelism => SeparatedValuesCpuBudget.Capacity;

    public static int Resolve(StructuredSchemaSnapshot snapshot, SourceExecutionContext executionContext)
    {
        return Resolve(snapshot.Identity.Length, executionContext);
    }

    public static int Resolve(SeparatedValuesSourceContract contract, SourceExecutionContext executionContext)
    {
        return Resolve(contract.Snapshot.Identity.Length, executionContext);
    }

    public static bool IsExplicitlyConfigured(IReadOnlyDictionary<string, string> settings)
    {
        return settings.TryGetValue(MaximumParallelismSettingName, out var text) &&
               !string.IsNullOrWhiteSpace(text) &&
               text != "0";
    }

    private static int Resolve(long fileLength, SourceExecutionContext executionContext)
    {
        var slicingAccepted = executionContext.Plan.AcceptedSkip.HasValue ||
                              executionContext.Plan.AcceptedTake.HasValue;
        if (slicingAccepted)
            return 1;

        var settings = executionContext.SourceRuntimeSettings;
        if (!settings.TryGetValue(MaximumParallelismSettingName, out var text) || string.IsNullOrWhiteSpace(text))
            return fileLength >= AutomaticCrossoverBytes ? AutomaticMaximumParallelism : 1;

        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var configured) || configured < 0)
        {
            throw new ArgumentException(
                $"Runtime setting '{MaximumParallelismSettingName}' must be a non-negative integer.",
                nameof(executionContext));
        }

        if (configured == 0)
            return fileLength >= AutomaticCrossoverBytes ? AutomaticMaximumParallelism : 1;
        return Math.Max(1, Math.Min(configured, AutomaticMaximumParallelism));
    }
}
