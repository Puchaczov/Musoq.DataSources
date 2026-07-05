using CsvHelper;

namespace Musoq.DataSources.SeparatedValues;

internal sealed class SeparatedValuesCsvParserFieldReader(CsvParser parser) : ISeparatedValuesFieldReader
{
    public int FieldCount => parser.Count;

    public string? GetField(int index)
    {
        return parser[index];
    }
}
