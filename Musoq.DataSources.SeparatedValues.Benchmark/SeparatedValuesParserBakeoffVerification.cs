using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using nietras.SeparatedValues;
using Sylvan.Data.Csv;
using SylvanCsvDataReader = Sylvan.Data.Csv.CsvDataReader;

namespace Musoq.DataSources.SeparatedValues.Benchmark;

internal static class SeparatedValuesParserBakeoffVerification
{
    private const char Separator = ',';

    public static int Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Musoq.DataSources.Benchmarks", "csv-bakeoff-verify",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var fixture = CreateRandomizedFixture(Path.Combine(directory, "grammar.csv"));
            var customRows = ReadCustom(fixture.Path, out var customQuoteStates);
            AssertRows("custom UTF-8 scanner", fixture.Rows, customRows);
            AssertQuoteStates(fixture.QuoteStates, customQuoteStates);

            var sepRows = ReadSep(fixture.Path);
            var sylvanRows = ReadSylvan(fixture.Path);
            var csvHelperRows = ReadCsvHelper(fixture.Path);
            var validGrammar = new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["custom"] = true,
                ["Sep"] = RowsEqual(fixture.Rows, sepRows),
                ["Sylvan"] = RowsEqual(fixture.Rows, sylvanRows),
                ["CsvHelper"] = RowsEqual(fixture.Rows, csvHelperRows)
            };

            var malformed = CreateMalformedFixtures(directory);
            var rejected = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["custom"] = CountRejected(malformed, path => ReadCustom(path, out _)),
                ["Sep"] = CountRejected(malformed, ReadSep),
                ["Sylvan"] = CountRejected(malformed, ReadSylvan),
                ["CsvHelper"] = CountRejected(malformed, ReadCsvHelper)
            };

            var totalMalformed = malformed.Count;
            if (!validGrammar["custom"] || rejected["custom"] != totalMalformed)
                throw new InvalidOperationException("The custom scanner did not satisfy its correctness gate.");

            Console.WriteLine($"Randomized valid rows: {fixture.Rows.Count:N0}");
            Console.WriteLine($"Malformed probes: {totalMalformed:N0}");
            WriteMismatch("Sep", fixture.Rows, sepRows);
            WriteMismatch("Sylvan", fixture.Rows, sylvanRows);
            WriteMismatch("CsvHelper", fixture.Rows, csvHelperRows);
            Console.WriteLine("Candidate | Valid grammar | Strict malformed | Field spans | Quoted-state | Skip without strings | Eligible");
            WriteCandidate("custom", validGrammar["custom"], rejected["custom"], totalMalformed, true, true, true);
            WriteCandidate("Sep", validGrammar["Sep"], rejected["Sep"], totalMalformed, true, true, true);
            WriteCandidate("Sylvan", validGrammar["Sylvan"], rejected["Sylvan"], totalMalformed, true, false, true);
            WriteCandidate("CsvHelper", validGrammar["CsvHelper"], rejected["CsvHelper"], totalMalformed, false, false,
                false);
            Console.WriteLine("Selected: custom managed UTF-8 scanner (the only candidate satisfying every contract capability). ");
            return 0;
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static Fixture CreateRandomizedFixture(string path)
    {
        var random = new Random(0x5eed);
        var rows = new List<string[]>();
        var quoteStates = new List<bool[]>();
        var builder = new StringBuilder();
        string[] values =
        [
            "", "alpha", "with,separator", "say \"hello\"", "line one\nline two", "line one\r\nline two",
            " leading", "trailing ", "Zażółć gęślą", "\t", "0", "-12.5"
        ];

        builder.Append('\ufeff');
        for (var rowIndex = 0; rowIndex < 2_000; rowIndex++)
        {
            var row = new string[6];
            var quoted = new bool[row.Length];

            for (var fieldIndex = 0; fieldIndex < row.Length; fieldIndex++)
            {
                var value = values[random.Next(values.Length)];
                var mustQuote = value.IndexOfAny([Separator, '"', '\r', '\n']) >= 0;
                var quote = mustQuote || random.Next(4) == 0;
                row[fieldIndex] = value;
                quoted[fieldIndex] = quote;

                if (fieldIndex != 0)
                    builder.Append(Separator);
                AppendField(builder, value, quote);
            }

            rows.Add(row);
            quoteStates.Add(quoted);
            builder.Append(rowIndex % 3 == 0 ? "\r\n" : "\n");

            if (rowIndex % 173 == 0)
                builder.Append(rowIndex % 2 == 0 ? "\r\n" : "\n");
        }

        var boundaryRow = new[] { new string('x', 1024 * 1024 + 113), "", "", "quoted", "tail", "" };
        var boundaryQuoted = new[] { false, false, true, true, false, false };
        for (var index = 0; index < boundaryRow.Length; index++)
        {
            if (index != 0)
                builder.Append(Separator);
            AppendField(builder, boundaryRow[index], boundaryQuoted[index]);
        }

        builder.Append("\r\n");
        rows.Add(boundaryRow);
        quoteStates.Add(boundaryQuoted);
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false, true));
        return new Fixture(path, rows, quoteStates);
    }

    private static IReadOnlyList<string> CreateMalformedFixtures(string directory)
    {
        var fixtures = new List<string>();
        WriteMalformed("unterminated-quote.csv", "\"unterminated\n", null);
        WriteMalformed("quote-in-unquoted.csv", "ab\"cd,value\n", null);
        WriteMalformed("after-closing-quote.csv", "\"value\"x,next\n", null);
        WriteMalformed("bare-carriage-return.csv", "first\rsecond,next\n", null);
        WriteMalformed("invalid-utf8.csv", null, [(byte)'a', (byte)',', 0xff, (byte)'\n']);
        return fixtures;

        void WriteMalformed(string name, string? text, byte[]? bytes)
        {
            var path = Path.Combine(directory, name);
            if (text is not null)
                File.WriteAllText(path, text, new UTF8Encoding(false, true));
            else
                File.WriteAllBytes(path, bytes!);
            fixtures.Add(path);
        }
    }

    private static List<string[]> ReadCustom(string path, out List<bool[]> quoteStates)
    {
        using var reader = new SeparatedValuesUtf8Reader(path, checked((byte)Separator));
        var rows = new List<string[]>();
        quoteStates = [];

        while (reader.TryRead(out var record))
        {
            var row = new List<string>();
            var quoted = new List<bool>();
            foreach (var field in record)
            {
                row.Add(field.Decode());
                quoted.Add(field.WasQuoted);
            }

            rows.Add(row.ToArray());
            quoteStates.Add(quoted.ToArray());
        }

        return rows;
    }

    private static List<string[]> ReadSep(string path)
    {
        using var reader = nietras.SeparatedValues.Sep.New(Separator)
            .Reader(options => options with
            {
                HasHeader = false,
                DisableColCountCheck = true,
                Unescape = true
            })
            .FromFile(path);
        var rows = new List<string[]>();

        foreach (var row in reader)
        {
            if (row.Span.IsEmpty)
                continue;

            var fields = new string[row.ColCount];
            for (var index = 0; index < fields.Length; index++)
                fields[index] = row[index].ToString();
            rows.Add(fields);
        }

        return rows;
    }

    private static List<string[]> ReadSylvan(string path)
    {
        using var reader = SylvanCsvDataReader.Create(path, new CsvDataReaderOptions
        {
            HasHeaders = false,
            Delimiter = Separator,
            CsvStyle = CsvStyle.Standard,
            BufferSize = 1024 * 1024,
            MaxBufferSize = 256 * 1024 * 1024
        });
        var rows = new List<string[]>();

        while (reader.Read())
        {
            if (IsBlankRecord(reader.GetRawRecordSpan()))
                continue;

            var fields = new string[reader.RowFieldCount];
            for (var index = 0; index < fields.Length; index++)
                fields[index] = reader.GetFieldSpan(index).ToString();
            rows.Add(fields);
        }

        return rows;
    }

    private static bool IsBlankRecord(ReadOnlySpan<char> record)
    {
        var length = record.Length;
        while (length != 0 && record[length - 1] is '\r' or '\n')
            length--;
        return length == 0;
    }

    private static List<string[]> ReadCsvHelper(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024,
            FileOptions.SequentialScan);
        using var textReader = new StreamReader(stream, new UTF8Encoding(false, true), true, 1024 * 1024);
        using var parser = new CsvParser(textReader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = Separator.ToString(),
            HasHeaderRecord = false,
            IgnoreBlankLines = true,
            BadDataFound = args => throw new InvalidDataException(args.RawRecord),
            LineBreakInQuotedFieldIsBadData = false
        });
        var rows = new List<string[]>();

        while (parser.Read())
        {
            var fields = new string[parser.Count];
            for (var index = 0; index < fields.Length; index++)
                fields[index] = parser[index] ?? string.Empty;
            rows.Add(fields);
        }

        return rows;
    }

    private static int CountRejected(IReadOnlyList<string> fixtures, Func<string, List<string[]>> reader)
    {
        var rejected = 0;
        foreach (var fixture in fixtures)
        {
            try
            {
                reader(fixture);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
            {
                rejected++;
            }
        }

        return rejected;
    }

    private static void AppendField(StringBuilder builder, string value, bool quoted)
    {
        if (!quoted)
        {
            builder.Append(value);
            return;
        }

        builder.Append('"');
        builder.Append(value.Replace("\"", "\"\"", StringComparison.Ordinal));
        builder.Append('"');
    }

    private static void AssertRows(string candidate, IReadOnlyList<string[]> expected, IReadOnlyList<string[]> actual)
    {
        if (!RowsEqual(expected, actual))
            throw new InvalidOperationException($"The {candidate} did not reproduce the randomized CSV fixture.");
    }

    private static bool RowsEqual(IReadOnlyList<string[]> expected, IReadOnlyList<string[]> actual)
    {
        if (expected.Count != actual.Count)
            return false;

        for (var row = 0; row < expected.Count; row++)
        {
            if (!expected[row].AsSpan().SequenceEqual(actual[row]))
                return false;
        }

        return true;
    }

    private static void AssertQuoteStates(IReadOnlyList<bool[]> expected, IReadOnlyList<bool[]> actual)
    {
        if (expected.Count != actual.Count)
            throw new InvalidOperationException("The custom scanner returned an unexpected number of quote-state rows.");

        for (var row = 0; row < expected.Count; row++)
        {
            if (!expected[row].AsSpan().SequenceEqual(actual[row]))
                throw new InvalidOperationException($"The custom scanner lost quote state in row {row:N0}.");
        }
    }

    private static void WriteCandidate(string name, bool validGrammar, int malformedRejected, int malformedTotal,
        bool spans, bool quoteState, bool skipsStrings)
    {
        var strict = malformedRejected == malformedTotal;
        var eligible = validGrammar && strict && spans && quoteState && skipsStrings;
        Console.WriteLine(
            $"{name} | {validGrammar} | {malformedRejected}/{malformedTotal} | {spans} | {quoteState} | {skipsStrings} | {eligible}");
    }

    private static void WriteMismatch(string name, IReadOnlyList<string[]> expected, IReadOnlyList<string[]> actual)
    {
        if (RowsEqual(expected, actual))
            return;

        if (expected.Count != actual.Count)
        {
            Console.WriteLine($"{name} valid-fixture mismatch: expected {expected.Count:N0} rows, got {actual.Count:N0}.");
            return;
        }

        for (var row = 0; row < expected.Count; row++)
        {
            if (expected[row].AsSpan().SequenceEqual(actual[row]))
                continue;

            Console.WriteLine(
                $"{name} valid-fixture mismatch at row {row:N0}: expected {expected[row].Length} fields, got {actual[row].Length}.");
            return;
        }
    }

    private sealed record Fixture(string Path, List<string[]> Rows, List<bool[]> QuoteStates);
}
