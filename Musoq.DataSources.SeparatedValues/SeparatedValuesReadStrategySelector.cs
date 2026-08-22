using System;

namespace Musoq.DataSources.SeparatedValues;

internal static class SeparatedValuesReadStrategySelector
{
    private const int TargetChunkBytes = 1024 * 1024;
    private const int MinimumChunkRows = 512;
    private const int MaximumChunkRows = 65536;
    private const int ZeroColumnChunkRows = 1024 * 1024;

    public static SeparatedValuesReadStrategy Select(SeparatedValuesReadStrategyContext context)
    {
        var projectedColumnCount = context.ProjectionAccepted
            ? context.ProjectedColumnCount
            : context.AllColumnCount;
        var rowChunkSize = projectedColumnCount == 0 && context.ProjectionAccepted
            ? ZeroColumnChunkRows
            : EstimateMaterializedChunkRows(projectedColumnCount);

        if (!context.HasResidualWork &&
            context.AcceptedTake.HasValue &&
            context.AcceptedTake.Value >= 0)
        {
            rowChunkSize = (int)Math.Min(
                rowChunkSize,
                Math.Max(1, context.AcceptedTake.Value));
        }

        return new SeparatedValuesReadStrategy(Math.Max(1, rowChunkSize));
    }

    private static int EstimateMaterializedChunkRows(int projectedColumnCount)
    {
        var columns = Math.Max(1, projectedColumnCount);
        var estimatedRowBytes = checked(32 + columns * 32);
        return Math.Clamp(TargetChunkBytes / estimatedRowBytes, MinimumChunkRows, MaximumChunkRows);
    }
}
