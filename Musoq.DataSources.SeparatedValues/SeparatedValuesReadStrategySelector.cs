using System;

namespace Musoq.DataSources.SeparatedValues;

internal static class SeparatedValuesReadStrategySelector
{
    private const int SmallFileChunkRows = 4096;
    private const int MediumFileChunkRows = 8192;
    private const int LargeFileChunkRows = 4096;
    private const int HugeFileChunkRows = 2048;
    private const int TargetChunkBytes = 1024 * 1024;
    private const int MinimumChunkRows = 512;
    private const int SmallTakeChunkLimit = 100000;
    private const long OneMebibyte = 1024L * 1024L;
    private const long SmallFileLimit = 128L * OneMebibyte;
    private const long MediumFileLimit = 2L * 1024L * OneMebibyte;
    private const long LargeFileLimit = 20L * 1024L * OneMebibyte;

    public static SeparatedValuesReadStrategy Select(SeparatedValuesReadStrategyContext context)
    {
        var profile = SelectProfile(context);
        var rowChunkSize = CapChunkRows(profile.ChunkRows, context);

        return new SeparatedValuesReadStrategy(rowChunkSize);
    }

    private static StrategyProfile SelectProfile(SeparatedValuesReadStrategyContext context)
    {
        var fileSize = context.FileSize;
        if (fileSize < SmallFileLimit)
            return new StrategyProfile(SmallFileChunkRows);

        if (fileSize <= MediumFileLimit)
            return new StrategyProfile(MediumFileChunkRows);

        if (fileSize <= LargeFileLimit)
            return new StrategyProfile(LargeFileChunkRows);

        return new StrategyProfile(HugeFileChunkRows);
    }

    private static int CapChunkRows(int baseChunkRows, SeparatedValuesReadStrategyContext context)
    {
        var projectedColumnCount = context.ProjectionAccepted
            ? context.ProjectedColumnCount
            : context.AllColumnCount;
        var estimatedRowBytes = Math.Max(64, Math.Max(1, projectedColumnCount) * 32);
        var memoryCappedRows = Math.Max(MinimumChunkRows, TargetChunkBytes / estimatedRowBytes);
        var chunkRows = Math.Min(baseChunkRows, memoryCappedRows);

        if (!context.HasResidualWork &&
            context.AcceptedTake.HasValue &&
            context.AcceptedTake.Value >= 0 &&
            context.AcceptedTake.Value <= SmallTakeChunkLimit)
            chunkRows = Math.Min(chunkRows, Math.Max(1, (int)context.AcceptedTake.Value));

        return Math.Max(1, chunkRows);
    }

    private readonly record struct StrategyProfile(int ChunkRows);
}
