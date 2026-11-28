#nullable enable

using System;

namespace Musoq.DataSources.SeparatedValues.Components.Execution;

internal static class SeparatedValuesFieldConversionFailure
{
    // Keep the diagnostic wire keys usable when compiling against the minimum supported Schema package.
    internal const string RecordNumberKey = "Musoq.DataSource.FieldConversion.RecordNumber";
    internal const string ColumnOrdinalKey = "Musoq.DataSource.FieldConversion.ColumnOrdinal";
    internal const string ExpectedTypeKey = "Musoq.DataSource.FieldConversion.ExpectedType";

    internal static FormatException Create(
        string message,
        long recordNumber,
        int sourceOrdinal,
        Type expectedType,
        Exception? innerException = null)
    {
        var failure = new FormatException(message, innerException);
        failure.Data[RecordNumberKey] = recordNumber;
        failure.Data[ColumnOrdinalKey] = sourceOrdinal + 1;
        failure.Data[ExpectedTypeKey] = expectedType;
        return failure;
    }

    internal static Type GetExpectedType(SeparatedValuesConversion conversion) => conversion switch
    {
        SeparatedValuesConversion.String => typeof(string),
        SeparatedValuesConversion.Boolean => typeof(bool),
        SeparatedValuesConversion.Byte => typeof(byte),
        SeparatedValuesConversion.Character => typeof(char),
        SeparatedValuesConversion.DateTime => typeof(DateTime),
        SeparatedValuesConversion.DateTimeOffset => typeof(DateTimeOffset),
        SeparatedValuesConversion.Decimal => typeof(decimal),
        SeparatedValuesConversion.Double => typeof(double),
        SeparatedValuesConversion.Int16 => typeof(short),
        SeparatedValuesConversion.Int32 => typeof(int),
        SeparatedValuesConversion.Int64 => typeof(long),
        SeparatedValuesConversion.SByte => typeof(sbyte),
        SeparatedValuesConversion.Single => typeof(float),
        SeparatedValuesConversion.TimeSpan => typeof(TimeSpan),
        SeparatedValuesConversion.UInt16 => typeof(ushort),
        SeparatedValuesConversion.UInt32 => typeof(uint),
        SeparatedValuesConversion.UInt64 => typeof(ulong),
        SeparatedValuesConversion.Guid => typeof(Guid),
        SeparatedValuesConversion.DateOnly => typeof(DateOnly),
        SeparatedValuesConversion.TimeOnly => typeof(TimeOnly),
        _ => typeof(object)
    };
}
