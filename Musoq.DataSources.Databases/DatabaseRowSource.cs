﻿using System.Collections.Concurrent;
using System.Data;
using Dapper;
using Musoq.DataSources.Databases.Helpers;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Databases;

public abstract class DatabaseRowSource : RowSourceBase<dynamic>
{
    private readonly RuntimeContext _runtimeContext;
    private readonly Func<IEnumerable<dynamic>>? _returnQuery;

    protected DatabaseRowSource(RuntimeContext runtimeContext, Func<IEnumerable<dynamic>>? returnQuery)
    {
        _runtimeContext = runtimeContext;
        _returnQuery = returnQuery;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        DatabaseHelpers.GetDataFromDatabase(
            chunkedSource,
            () => CreateConnection(_runtimeContext.EnvironmentVariables),
            CreateQueryCommand,
            (query, connection) => _returnQuery?.Invoke() ?? connection.Query(query),
            _runtimeContext.EndWorkToken);
    }

    protected abstract IDbConnection CreateConnection(IReadOnlyDictionary<string, string> environmentVariables);

    protected abstract string CreateQueryCommand();
}