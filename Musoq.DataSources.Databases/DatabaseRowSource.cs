using System.Collections.Concurrent;
using System.Data;
using Dapper;
using Musoq.DataSources.Databases.Helpers;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Databases;

public abstract class DatabaseRowSource : RowSourceBase<dynamic>
{
    private const string DatabaseSourceName = "database";
    private readonly RuntimeContext _runtimeContext;
    private readonly Func<IEnumerable<dynamic>>? _returnQuery;

    protected DatabaseRowSource(RuntimeContext runtimeContext, Func<IEnumerable<dynamic>>? returnQuery)
    {
        _runtimeContext = runtimeContext;
        _returnQuery = returnQuery;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        _runtimeContext.ReportDataSourceBegin(DatabaseSourceName);
        long totalRowsProcessed = 0;
        
        try
        {
            totalRowsProcessed = DatabaseHelpers.GetDataFromDatabase(
                chunkedSource,
                CreateConnection,
                CreateQueryCommand,
                (query, connection) => _returnQuery?.Invoke() ?? connection.Query(query),
                _runtimeContext.EndWorkToken);
        }
        finally
        {
            _runtimeContext.ReportDataSourceEnd(DatabaseSourceName, totalRowsProcessed);
        }
    }

    protected abstract IDbConnection CreateConnection();

    protected abstract string CreateQueryCommand();
}