using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;

namespace Musoq.DataSources.SeparatedValues.Benchmark;

internal static class FrozenCsvHelperSeparatedValuesAdapter
{
    public static IEnumerable<object?[]> Read(string path, string delimiter)
    {
        using var stream = File.OpenRead(path);
        using var textReader = new StreamReader(stream, new UTF8Encoding(false, true), false, 1024 * 1024);
        using var parser = new CsvParser(textReader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter,
            HasHeaderRecord = false,
            BadDataFound = null
        });

        while (parser.Read())
        {
            var first = parser[0];
            var second = parser[1];
            yield return
            [
                string.IsNullOrEmpty(first) ? null : first,
                string.IsNullOrEmpty(second) ? null : second
            ];
        }
    }
}
