#nullable enable

using System;

using Musoq.DataSources.Search.Components.Diagnostics;

namespace Musoq.DataSources.Search.Components.Text;

internal enum SearchCaseMode
{
    Sensitive,
    Insensitive
}

internal static class SearchCasePolicy
{
    public static SearchCaseMode ParseOptional(string? value)
    {
        return value is null ? SearchCaseMode.Sensitive : Parse(value);
    }

    public static SearchCaseMode Parse(string? value)
    {
        if (value is null)
        {
            throw new SearchRequestException(
                SearchDiagnosticCatalog.InvalidArgument("case"));
        }

        if (value.Equals("sensitive", StringComparison.OrdinalIgnoreCase))
            return SearchCaseMode.Sensitive;

        if (value.Equals("insensitive", StringComparison.OrdinalIgnoreCase))
            return SearchCaseMode.Insensitive;

        throw new SearchRequestException(
            SearchDiagnosticCatalog.InvalidArgument("case"));
    }

    public static string ToContractValue(SearchCaseMode mode)
    {
        return mode switch
        {
            SearchCaseMode.Sensitive => "sensitive",
            SearchCaseMode.Insensitive => "insensitive",
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }
}
