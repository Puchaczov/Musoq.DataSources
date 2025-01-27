using Musoq.Schema;

namespace Musoq.DataSources.Postgres.Tests.Components;

public class TestsPostgresSchemaProvider : ISchemaProvider
{
    private readonly dynamic[] _columns;
    private readonly dynamic[] _rows;

    public TestsPostgresSchemaProvider(dynamic[] columns, dynamic[] rows)
    {
        _columns = columns;
        _rows = rows;
    }

    public ISchema GetSchema(string schema)
    {
        return new TestsPostgresSchema(_columns, _rows);
    }
}