using Musoq.Schema;

namespace Musoq.DataSources.Sqlite.Tests.Components;

internal class TestsSqliteSchemaProvider : ISchemaProvider
{
    public ISchema GetSchema(string schema)
    {
        return new TestsSqliteSchema();
    }
}