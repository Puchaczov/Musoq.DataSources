#nullable enable

using System;

using Musoq.DataSources.Search.Components.Diagnostics;

namespace Musoq.DataSources.Search.Components.Text;

internal enum SearchEncodingMode
{
    Auto,
    Utf8,
    Utf8Bom,
    Utf16LittleEndianBom,
    Utf16BigEndianBom
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
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }
}
