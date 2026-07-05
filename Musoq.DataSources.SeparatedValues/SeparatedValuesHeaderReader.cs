using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;

namespace Musoq.DataSources.SeparatedValues;

internal static class SeparatedValuesHeaderReader
{
    public static string?[] ReadFirstRecord(
        FileInfo file,
        string separator,
        int skipLines,
        int bufferSize)
    {
        using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize,
            FileOptions.SequentialScan);
        using var reader = new StreamReader(stream, Encoding.UTF8, true, bufferSize);

        SkipLines(reader, skipLines);

        using var csvReader = new CsvReader(
            reader,
            SeparatedValuesCsvConfigurationFactory.Create(separator, bufferSize, false));
        if (!csvReader.Read())
            return [];

        return csvReader.Context.Parser!.Record ?? [];
    }

    public static async Task<string?[]> ReadFirstRecordAsync(
        FileInfo file,
        string separator,
        int skipLines,
        int bufferSize)
    {
        await using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize, FileOptions.SequentialScan);
        using var reader = new StreamReader(stream, Encoding.UTF8, true, bufferSize);

        await SkipLinesAsync(reader, skipLines);

        using var csvReader = new CsvReader(
            reader,
            SeparatedValuesCsvConfigurationFactory.Create(separator, bufferSize, false));
        if (!await csvReader.ReadAsync())
            return [];

        return csvReader.Context.Parser!.Record ?? [];
    }

    public static Dictionary<int, string> CreateIndexToNameMap(
        IReadOnlyList<string?> record,
        bool hasHeader)
    {
        var indexToNameMap = new Dictionary<int, string>();

        for (var i = 0; i < record.Count; ++i)
        {
            var headerName = hasHeader
                ? SeparatedValuesHelper.MakeHeaderNameValidColumnName(record[i] ?? string.Empty)
                : string.Format(SeparatedValuesHelper.AutoColumnName, i + 1);
            indexToNameMap.Add(i, headerName);
        }

        return indexToNameMap;
    }

    private static void SkipLines(TextReader reader, int linesToSkip)
    {
        for (var i = 0; i < linesToSkip; i++)
            reader.ReadLine();
    }

    private static async Task SkipLinesAsync(TextReader reader, int linesToSkip)
    {
        for (var i = 0; i < linesToSkip; i++)
            await reader.ReadLineAsync();
    }
}
