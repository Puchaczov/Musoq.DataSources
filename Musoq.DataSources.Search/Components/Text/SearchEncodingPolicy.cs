#nullable enable

using System;
using System.Text;

using Musoq.DataSources.Search.Components.Diagnostics;

namespace Musoq.DataSources.Search.Components.Text;

internal enum SearchEncodingMode
{
    Auto,
    Utf8,
    Utf8Bom,
    Utf16LittleEndianBom,
    Utf16BigEndianBom,
    Windows1250,
    Windows1252,
    Iso88592
}

internal static class SearchEncodingPolicy
{
    public static SearchEncodingMode ParseOptional(string? value)
    {
        return value is null ? SearchEncodingMode.Auto : Parse(value);
    }

    public static SearchEncodingMode Parse(string? value)
    {
        if (value is null)
        {
            throw new SearchEncodingException(
                SearchDiagnosticCatalog.InvalidArgument("encoding"));
        }

        if (value.Equals("auto", StringComparison.OrdinalIgnoreCase))
            return SearchEncodingMode.Auto;

        if (value.Equals("utf8", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("utf-8", StringComparison.OrdinalIgnoreCase))
        {
            return SearchEncodingMode.Utf8;
        }

        if (value.Equals("utf8-bom", StringComparison.OrdinalIgnoreCase))
            return SearchEncodingMode.Utf8Bom;

        if (value.Equals("utf16-le-bom", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("utf-16le", StringComparison.OrdinalIgnoreCase))
        {
            return SearchEncodingMode.Utf16LittleEndianBom;
        }

        if (value.Equals("utf16-be-bom", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("utf-16be", StringComparison.OrdinalIgnoreCase))
        {
            return SearchEncodingMode.Utf16BigEndianBom;
        }

        if (value.Equals("windows-1250", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("windows1250", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("cp1250", StringComparison.OrdinalIgnoreCase))
        {
            return SearchEncodingMode.Windows1250;
        }

        if (value.Equals("windows-1252", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("windows1252", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("cp1252", StringComparison.OrdinalIgnoreCase))
        {
            return SearchEncodingMode.Windows1252;
        }

        if (value.Equals("iso-8859-2", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("iso8859-2", StringComparison.OrdinalIgnoreCase))
        {
            return SearchEncodingMode.Iso88592;
        }

        throw new SearchEncodingException(SearchDiagnosticCatalog.UnsupportedEncoding());
    }

    public static string ToContractValue(SearchEncodingMode mode)
    {
        return mode switch
        {
            SearchEncodingMode.Auto => "auto",
            SearchEncodingMode.Utf8 => "utf8",
            SearchEncodingMode.Utf8Bom => "utf8-bom",
            SearchEncodingMode.Utf16LittleEndianBom => "utf16-le-bom",
            SearchEncodingMode.Utf16BigEndianBom => "utf16-be-bom",
            SearchEncodingMode.Windows1250 => "windows-1250",
            SearchEncodingMode.Windows1252 => "windows-1252",
            SearchEncodingMode.Iso88592 => "iso-8859-2",
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }

    public static Encoding CreateLegacyEncoding(SearchEncodingMode mode)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return mode switch
        {
            SearchEncodingMode.Windows1250 => Encoding.GetEncoding(
                1250,
                EncoderFallback.ExceptionFallback,
                DecoderFallback.ExceptionFallback),
            SearchEncodingMode.Windows1252 => Encoding.GetEncoding(
                1252,
                EncoderFallback.ExceptionFallback,
                DecoderFallback.ExceptionFallback),
            SearchEncodingMode.Iso88592 => Encoding.GetEncoding(
                28592,
                EncoderFallback.ExceptionFallback,
                DecoderFallback.ExceptionFallback),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }
}
