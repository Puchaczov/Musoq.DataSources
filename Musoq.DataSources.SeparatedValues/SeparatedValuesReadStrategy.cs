namespace Musoq.DataSources.SeparatedValues;

internal sealed class SeparatedValuesReadStrategy(
    int streamBufferSize,
    int rowChunkSize,
    bool avoidSecondHeaderOpen,
    bool enableZeroColumnFastPath,
    bool enableEarlyTakeFastPath)
{
    public int StreamBufferSize { get; } = streamBufferSize;

    public int RowChunkSize { get; } = rowChunkSize;

    public bool AvoidSecondHeaderOpen { get; } = avoidSecondHeaderOpen;

    public bool EnableZeroColumnFastPath { get; } = enableZeroColumnFastPath;

    public bool EnableEarlyTakeFastPath { get; } = enableEarlyTakeFastPath;
}
