using Musoq.Schema;

namespace Musoq.DataSources.Sqlite.Tests.Components;

internal class TestsSqliteRowSource : SqliteRowSource
{
    public TestsSqliteRowSource(RuntimeContext runtimeContext) 
        : base(runtimeContext)
    {
    }
}