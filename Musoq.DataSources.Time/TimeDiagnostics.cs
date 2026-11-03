using System;
using Musoq.Schema.Diagnostics;

namespace Musoq.DataSources.Time;

internal static class TimeDiagnostics
{
    internal static DateTimeOffset ReadDate(string value, string argumentName, int argumentIndex)
    {
        try { return DateTimeOffset.Parse(value); }
        catch (FormatException error) { throw InvalidArgument(argumentName, argumentIndex, error); }
    }

    internal static DataSourceException InvalidArgument(string argumentName, int argumentIndex, Exception? privateException = null)
        => new(new DataSourceDiagnostic("TIME.InvalidArgument", "input", "The time interval argument is invalid.",
            new DataSourceDiagnosticOptions
            {
                FailureKind = DataSourceFailureKind.InvalidInput,
                ArgumentName = argumentName,
                ArgumentIndex = argumentIndex,
                SuggestedFixes = ["Provide valid start/stop date-time values and a supported interval: seconds, minutes, hours, days, months, or years."]
            }), privateException);
}
