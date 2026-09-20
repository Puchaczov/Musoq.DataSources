#nullable enable

namespace Musoq.DataSources.Search.Testing;

/// <summary>
///     Canonical result formatting for contract comparisons. It removes row
///     order from comparisons only when the query did not promise an order.
/// </summary>
public static class ResultSignature
{
    public static string ForRows(
        IEnumerable<IReadOnlyList<object?>> rows,
        Func<IReadOnlyList<object?>, string>? formatter = null)
    {
        ArgumentNullException.ThrowIfNull(rows);

        formatter ??= FormatRow;
        return string.Join(
            "\n",
            rows.Select(formatter).OrderBy(value => value, StringComparer.Ordinal));
    }

    public static string ForColumns(
        SearchQueryResult result,
        params int[] columns)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(columns);

        return ForRows(result.Rows, row => string.Join(
            "|",
            columns.Select(column => FormatValue(row[column]))));
    }

    public static string NormalizePath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return path.Replace('\\', '/');
    }

    public static string FormatValue(object? value)
    {
        return value switch
        {
            null => "<null>",
            byte[] bytes => Convert.ToHexString(bytes),
            IEnumerable<object?> values => "[" + string.Join(",", values.Select(FormatValue)) + "]",
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string FormatRow(IReadOnlyList<object?> row)
    {
        return string.Join("|", row.Select(FormatValue));
    }
}
