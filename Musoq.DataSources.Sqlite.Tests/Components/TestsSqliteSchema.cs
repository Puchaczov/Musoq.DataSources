using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Sqlite.Tests.Components;

internal class TestsSqliteSchema : SqliteSchema
{
    public override ISchemaTable GetTableByName(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        return new TestsSqliteTable(runtimeContext);
    }

    public override RowSource GetRowSource(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        return new TestsSqliteRowSource(runtimeContext);
    }
}