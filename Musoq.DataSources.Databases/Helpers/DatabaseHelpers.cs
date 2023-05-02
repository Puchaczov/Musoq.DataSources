using System.Collections.Concurrent;
using System.Data;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Databases.Helpers;

public static class DatabaseHelpers
{
    public static void GetDataFromDatabase(
        BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource,
        Func<IDbConnection> createConnection,
        Func<string> createQueryCommand,
        Func<string, IDbConnection, IEnumerable<dynamic>?> connectionQuery,
        CancellationToken cancellationToken = default)
    {
        using var connection = createConnection();

        connection.Open();

        var query = createQueryCommand();

        var result = connectionQuery(query, connection);

        if (result == null)
            return;

        using var enumerator = result.GetEnumerator();

        if (!enumerator.MoveNext())
            return;

        if (enumerator.Current is not IDictionary<string, object> firstRow)
            return;

        var index = 0;
        var indexToNameMap = firstRow.Keys.ToDictionary(_ => index++);

        var list = new List<IObjectResolver>
        {
            new DynamicObjectResolver(firstRow, indexToNameMap)
        };

        while (enumerator.MoveNext())
        {
            if (enumerator.Current is not IDictionary<string, object> row)
                continue;

            list.Add(new DynamicObjectResolver(row, indexToNameMap));

            if (list.Count < 1000)
                continue;

            chunkedSource.Add(list, cancellationToken);

            list = new List<IObjectResolver>(1000);
        }

        chunkedSource.Add(list, cancellationToken);
    }
}