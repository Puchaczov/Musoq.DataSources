using System.Collections.Generic;
using System.Data;
using Moq;
using Musoq.Schema;

namespace Musoq.DataSources.Postgres.Tests.Components;

internal class TestsPostgresRowSource : PostgresRowSource
{
    public TestsPostgresRowSource(RuntimeContext runtimeContext, string schema, IEnumerable<dynamic> returnedEntities) 
        : base(runtimeContext, schema, () => returnedEntities)
    {
    }

    protected override IDbConnection CreateConnection(IReadOnlyDictionary<string, string> environmentVariables)
    {
        var mock = new Mock<IDbConnection>();
        
        mock.Setup(connection => connection.Open());
        mock.Setup(connection => connection.Close());
        
        return mock.Object;
    }
}