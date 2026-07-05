using System;
using System.Globalization;
using CsvHelper.Configuration;

namespace Musoq.DataSources.SeparatedValues;

internal static class SeparatedValuesCsvConfigurationFactory
{
    public const int MaximumParserBufferSize = 256 * 1024;
    public const int ProcessFieldBufferSize = 4096;

    public static CsvConfiguration Create(
        string separator,
        int streamBufferSize,
        bool tolerateBadData)
    {
        var culture = new CultureInfo(CultureInfo.CurrentCulture.Name)
        {
            TextInfo = { ListSeparator = separator }
        };

        return Create(culture, streamBufferSize, tolerateBadData);
    }

    public static CsvConfiguration Create(
        CultureInfo culture,
        int streamBufferSize,
        bool tolerateBadData)
    {
        var configuration = new CsvConfiguration(culture)
        {
            BufferSize = Math.Min(streamBufferSize, MaximumParserBufferSize),
            ProcessFieldBufferSize = ProcessFieldBufferSize,
            CountBytes = false,
            DetectDelimiter = false,
            DetectColumnCountChanges = false,
            TrimOptions = TrimOptions.None,
            ExceptionMessagesContainRawData = false
        };

        if (tolerateBadData)
            configuration.BadDataFound = _ => { };

        return configuration;
    }
}
