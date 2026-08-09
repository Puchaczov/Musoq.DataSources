namespace Musoq.DataSources.SeparatedValues;

internal sealed class SeparatedValuesReadStrategy(int rowChunkSize)
{
    public int RowChunkSize { get; } = rowChunkSize;
}
