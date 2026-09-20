namespace Musoq.DataSources.SeparatedValues.Components.Planning;

internal sealed class SeparatedValuesReadStrategy(int rowChunkSize)
{
    public int RowChunkSize { get; } = rowChunkSize;
}
