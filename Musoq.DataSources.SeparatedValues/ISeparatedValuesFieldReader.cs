namespace Musoq.DataSources.SeparatedValues;

internal interface ISeparatedValuesFieldReader
{
    int FieldCount { get; }

    string? GetField(int index);
}
