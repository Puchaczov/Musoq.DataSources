using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Managers;

namespace Musoq.DataSources.Sqlite;

public class SqliteSchema : SchemaBase
{
    private const string SchemaName = "sqlite";
    
    public SqliteSchema() 
        : base(SchemaName, CreateLibrary())
    {
    }
    
    public override ISchemaTable GetTableByName(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        return new SqliteTable(runtimeContext);
    }

    public override RowSource GetRowSource(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        return new SqliteRowSource(runtimeContext);
    }

    private static MethodsAggregator CreateLibrary()
    {
        var methodsManager = new MethodsManager();
        var propertiesManager = new PropertiesManager();

        var library = new SqliteLibrary();

        methodsManager.RegisterLibraries(library);
        propertiesManager.RegisterProperties(library);

        return new MethodsAggregator(methodsManager, propertiesManager);
    }
}