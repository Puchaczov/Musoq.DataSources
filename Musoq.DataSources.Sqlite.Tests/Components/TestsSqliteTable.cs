using Musoq.Schema;

namespace Musoq.DataSources.Sqlite.Tests.Components;

internal class TestsSqliteTable : SqliteTable
{
    public TestsSqliteTable(RuntimeContext runtimeContext)
        : base(runtimeContext)
    {
    }
}