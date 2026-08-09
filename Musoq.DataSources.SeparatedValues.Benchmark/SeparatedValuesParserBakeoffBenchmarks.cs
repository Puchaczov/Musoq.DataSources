using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using CsvHelper;
using CsvHelper.Configuration;
using nietras.SeparatedValues;
using Sylvan.Data.Csv;
using SylvanCsvDataReader = Sylvan.Data.Csv.CsvDataReader;

namespace Musoq.DataSources.SeparatedValues.Benchmark;

public enum SeparatedValuesParserScenario
{
    OneBrc,
    Wide,
    QuotedMultiline
}

[MemoryDiagnoser]
[ShortRunJob]
public class SeparatedValuesParserBakeoffBenchmarks
{
    private string _path = null!;
    private char _separator;

    [Params(100_000)]
    public int RowCount { get; set; }

    [ParamsAllValues]
    public SeparatedValuesParserScenario Scenario { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        (_path, _separator) = Scenario switch
        {
            SeparatedValuesParserScenario.OneBrc =>
                (SeparatedValuesBenchmarkData.EnsureOneBrcFile(RowCount), ';'),
            SeparatedValuesParserScenario.Wide =>
                (SeparatedValuesBenchmarkData.EnsureWideFile(RowCount), ','),
            SeparatedValuesParserScenario.QuotedMultiline =>
                (SeparatedValuesBenchmarkData.EnsureQuotedMultilineFile(RowCount), ','),
            _ => throw new ArgumentOutOfRangeException(nameof(Scenario), Scenario, null)
        };
    }

    [Benchmark(Baseline = true)]
    public ParserScanResult CustomUtf8Scanner()
    {
        return Utf8SeparatedValuesScanner.Scan(_path, checked((byte)_separator));
    }

    [Benchmark]
    public ParserScanResult Sep()
    {
        using var reader = nietras.SeparatedValues.Sep.New(_separator)
            .Reader(options => options with
            {
                HasHeader = false,
                DisableColCountCheck = true,
                Unescape = true
            })
            .FromFile(_path);
        var accumulator = new ParserScanAccumulator();

        foreach (var row in reader)
        {
            if (row.Span.IsEmpty)
                continue;

            accumulator.AddRecord();
            for (var index = 0; index < row.ColCount; index++)
                accumulator.Add(row[index].Span);
        }

        return accumulator.ToResult();
    }

    [Benchmark]
    public ParserScanResult Sylvan()
    {
        using var reader = SylvanCsvDataReader.Create(_path, new CsvDataReaderOptions
        {
            HasHeaders = false,
            Delimiter = _separator,
            CsvStyle = CsvStyle.Standard,
            BufferSize = 1024 * 1024,
            MaxBufferSize = 256 * 1024 * 1024
        });
        var accumulator = new ParserScanAccumulator();

        while (reader.Read())
        {
            if (IsBlankRecord(reader.GetRawRecordSpan()))
                continue;

            accumulator.AddRecord();
            for (var index = 0; index < reader.RowFieldCount; index++)
                accumulator.Add(reader.GetFieldSpan(index));
        }

        return accumulator.ToResult();
    }

    private static bool IsBlankRecord(ReadOnlySpan<char> record)
    {
        var length = record.Length;
        while (length != 0 && record[length - 1] is '\r' or '\n')
            length--;
        return length == 0;
    }

    [Benchmark]
    public ParserScanResult CsvHelper()
    {
        using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024,
            FileOptions.SequentialScan);
        using var textReader = new StreamReader(stream, new UTF8Encoding(false, true), false, 1024 * 1024);
        using var parser = new CsvParser(textReader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = _separator.ToString(),
            HasHeaderRecord = false,
            BadDataFound = args => throw new InvalidDataException(args.RawRecord),
            LineBreakInQuotedFieldIsBadData = false
        });
        var accumulator = new ParserScanAccumulator();

        while (parser.Read())
        {
            accumulator.AddRecord();
            for (var index = 0; index < parser.Count; index++)
                accumulator.Add(parser[index]);
        }

        return accumulator.ToResult();
    }
}
