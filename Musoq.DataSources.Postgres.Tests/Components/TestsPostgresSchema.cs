using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Postgres.Tests.Components;

public class TestsPostgresSchema : PostgresSchema
{
    private readonly dynamic[]? _columns;
    private readonly dynamic[]? _rows;
    
    public TestsPostgresSchema(dynamic[] columns, dynamic[] rows)
    {
        _columns = columns;
        _rows = rows;
    }
    
    public override ISchemaTable GetTableByName(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        return new TestsPostgresTable(runtimeContext, name, _columns ?? []);
    }

    public override RowSource GetRowSource(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        return new TestsPostgresRowSource(runtimeContext, name, _rows ?? []);
    }
}