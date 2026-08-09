using Musoq.DataSources.SeparatedValues;

namespace Musoq.DataSources.SeparatedValues.Benchmark;

internal static class Utf8SeparatedValuesScanner
{
    public static ParserScanResult Scan(string path, byte separator)
    {
        using var reader = new SeparatedValuesUtf8Reader(path, separator);
        var accumulator = new ParserScanAccumulator();

        while (reader.TryRead(out var record))
        {
            accumulator.AddRecord();
            foreach (var field in record)
                accumulator.Add(field);
        }

        return accumulator.ToResult();
    }
}

public readonly record struct ParserScanResult(long Records, long Fields, long ValueLength, long EdgeChecksum,
    long QuotedFields, long UnquotedEmptyFields, long QuotedEmptyFields);

internal struct ParserScanAccumulator
{
    private long _records;
    private long _fields;
    private long _valueLength;
    private long _edgeChecksum;
    private long _quotedFields;
    private long _unquotedEmptyFields;
    private long _quotedEmptyFields;

    public void AddRecord()
    {
        _records++;
    }

    public void Add(SeparatedValuesUtf8Field field)
    {
        _fields++;
        if (field.WasQuoted)
            _quotedFields++;

        if (field.EncodedValue.IsEmpty)
        {
            if (field.WasQuoted)
                _quotedEmptyFields++;
            else
                _unquotedEmptyFields++;
        }

        if (!field.NeedsUnescaping)
        {
            AddValue(field.EncodedValue.Length, field.EncodedValue.IsEmpty ? (byte)0 : field.EncodedValue[0],
                field.EncodedValue.IsEmpty ? (byte)0 : field.EncodedValue[^1], !field.EncodedValue.IsEmpty);
            return;
        }

        var length = 0;
        byte first = 0;
        byte last = 0;
        var hasValue = false;

        for (var offset = 0; offset < field.EncodedValue.Length; offset++)
        {
            var value = field.EncodedValue[offset];
            if (!hasValue)
            {
                first = value;
                hasValue = true;
            }

            last = value;
            length++;

            if (value == (byte)'"' &&
                offset + 1 < field.EncodedValue.Length && field.EncodedValue[offset + 1] == (byte)'"')
                offset++;
        }

        AddValue(length, first, last, hasValue);
    }

    public void Add(ReadOnlySpan<char> value)
    {
        _fields++;
        AddValue(value.Length, value.IsEmpty ? (byte)0 : checked((byte)value[0]),
            value.IsEmpty ? (byte)0 : checked((byte)value[^1]), !value.IsEmpty);
    }

    public void Add(string? value)
    {
        Add(value.AsSpan());
    }

    public ParserScanResult ToResult()
    {
        return new ParserScanResult(_records, _fields, _valueLength, _edgeChecksum, _quotedFields,
            _unquotedEmptyFields, _quotedEmptyFields);
    }

    private void AddValue(int length, byte first, byte last, bool hasValue)
    {
        _valueLength += length;
        unchecked
        {
            _edgeChecksum = _edgeChecksum * 397 ^ length;
            if (hasValue)
                _edgeChecksum = (_edgeChecksum * 397 ^ first) * 397 ^ last;
        }
    }
}
