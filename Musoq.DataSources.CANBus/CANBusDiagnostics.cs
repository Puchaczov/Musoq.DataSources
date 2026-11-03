#nullable enable

using System;
using CsvHelper;
using CsvHelper.TypeConversion;
using Musoq.Schema.Diagnostics;

namespace Musoq.DataSources.CANBus;

internal static class CANBusDiagnostics
{
    internal static DataSourceException? Map(Exception error)
        => error is BadDataException or HeaderValidationException or CsvHelper.MissingFieldException or TypeConverterException
            ? new DataSourceException(new DataSourceDiagnostic("CANBUS.InvalidCapture", "format",
                "The CAN capture contains an invalid field or record.", new DataSourceDiagnosticOptions
                {
                    FailureKind = DataSourceFailureKind.InvalidInput,
                    SuggestedFixes = ["Check the capture delimiter, headers, field types, and selected frame format."]
                }), error)
            : null;
}
