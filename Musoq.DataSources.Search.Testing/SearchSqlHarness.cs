#nullable enable

using Musoq.Converter;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Schema;

namespace Musoq.DataSources.Search.Testing;

public sealed class SearchSqlHarness
{
    public SearchQueryResult Execute(
        string query,
        IReadOnlyDictionary<string, string>? runtimeSettings = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ValidateQuery(query);

        var effectiveSettings = runtimeSettings ?? new Dictionary<string, string>();

        try
        {
            var compiled = InstanceCreatorHelpers.CompileForExecution(
                query,
                $"search-contract-{Guid.NewGuid():N}",
                new SearchSchemaProvider(),
                EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables(effectiveSettings));

            var result = compiled.Run();
            var rows = result.Rows
                .Select(row => (IReadOnlyList<object?>)row.Values.Cast<object?>().ToArray())
                .ToArray();

            return new SearchQueryResult(rows);
        }
        catch (Exception exception)
        {
            exception.Data["SearchQuery"] = query;
            exception.Data["SearchRuntimeSettings"] = string.Join(
                ",",
                effectiveSettings.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => $"{pair.Key}={pair.Value}"));
            throw;
        }
    }

    public SearchQueryResult ExecuteForRoot(
        string root,
        Func<string, string> queryFactory,
        IReadOnlyDictionary<string, string>? runtimeSettings = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(queryFactory);
        return Execute(queryFactory(EscapeSqlLiteral(root)), runtimeSettings);
    }

    public static string EscapeSqlLiteral(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("'", "''", StringComparison.Ordinal);
    }

    public static IReadOnlyList<T> MaterializeTyped<T>(
        IEnumerable<IReadOnlyList<T>> chunks)
    {
        ArgumentNullException.ThrowIfNull(chunks);
        return chunks.SelectMany(static chunk => chunk).ToArray();
    }

    public static void ValidateQuery(string query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Contains('#', StringComparison.Ordinal))
            throw new ArgumentException("Search contract queries must not contain the '#' character.", nameof(query));
    }

    private sealed class SearchSchemaProvider : ISchemaProvider
    {
        public ISchema GetSchema(string schema)
        {
            return new SearchSchema();
        }
    }
}

public sealed class SearchQueryResult
{
    internal SearchQueryResult(IReadOnlyList<IReadOnlyList<object?>> rows)
    {
        Rows = rows;
    }

    public IReadOnlyList<IReadOnlyList<object?>> Rows { get; }

    public int Count => Rows.Count;

    public object? Value(int row, int column)
    {
        return Rows[row][column];
    }

    public T Value<T>(int row, int column)
    {
        return (T)Rows[row][column]!;
    }

    public string Signature(Func<IReadOnlyList<object?>, string>? rowFormatter = null)
    {
        rowFormatter ??= static row => string.Join(
            "|",
            row.Select(value => value switch
            {
                null => "<null>",
                byte[] bytes => Convert.ToHexString(bytes),
                IEnumerable<object> values => "[" + string.Join(",", values) + "]",
                _ => value.ToString()
            }));

        return string.Join("\n", Rows.Select(rowFormatter));
    }
}
