using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Managers;

namespace Musoq.DataSources.Postgres;

public class PostgresSchema : SchemaBase
{
    private const string SchemaName = "postgres";
    
    public PostgresSchema() 
        : base(SchemaName, CreateLibrary())
    {
    }
    
    public override ISchemaTable GetTableByName(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        return new PostgresTable(runtimeContext, (string)parameters[0]);
    }

    public override RowSource GetRowSource(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        return new PostgresRowSource(runtimeContext, (string)parameters[0]);
    }

    private static MethodsAggregator CreateLibrary()
    {
        var methodsManager = new MethodsManager();
        var propertiesManager = new PropertiesManager();

        var library = new PostgresLibrary();

        methodsManager.RegisterLibraries(library);
        propertiesManager.RegisterProperties(library);

        return new MethodsAggregator(methodsManager, propertiesManager);
    }
}