#nullable enable

namespace Musoq.DataSources.Search.Components.Text;

internal readonly record struct SearchCharCoordinate(
    bool IsMapped,
    long ByteOffset,
    long ByteLength,
    bool CanStart,
    bool CanEnd)
{
    public long ByteEndExclusive => checked(ByteOffset + ByteLength);
}

internal interface ISearchTextCoordinateReader
{
    long InitialByteOffset { get; }

    SearchCharCoordinate[] LastReadCoordinates { get; }

    int LastReadCoordinateCount { get; }
}
