#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;

namespace Musoq.DataSources.Structured;

internal static class StructuredParallelism
{
    public static int ResolveMaximum(
        IReadOnlyDictionary<string, string> settings,
        string settingName,
        int automaticMaximum,
        int partitionCount,
        bool automaticEligible)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(settingName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(automaticMaximum);
        ArgumentOutOfRangeException.ThrowIfNegative(partitionCount);

        if (!settings.TryGetValue(settingName, out var text) || string.IsNullOrWhiteSpace(text))
            return automaticEligible ? Cap(automaticMaximum, partitionCount) : 1;

        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var configured) || configured < 0)
        {
            throw new ArgumentException(
                $"Runtime setting '{settingName}' must be a non-negative integer.",
                nameof(settings));
        }

        if (configured == 0)
            return automaticEligible ? Cap(automaticMaximum, partitionCount) : 1;
        return Cap(configured, partitionCount);
    }

    private static int Cap(int requested, int partitionCount)
    {
        return Math.Max(1, Math.Min(requested, Math.Min(Environment.ProcessorCount, Math.Max(1, partitionCount))));
    }
}
