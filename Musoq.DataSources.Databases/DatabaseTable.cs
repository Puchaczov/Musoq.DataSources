using System.Collections.Concurrent;
using System.Data;
using Dapper;
using Musoq.DataSources.Databases.Helpers;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Databases;

public abstract class DatabaseTable : ISchemaTable
{
    private readonly RuntimeContext _runtimeContext;
    
    protected DatabaseTable(RuntimeContext runtimeContext)
    {
        _runtimeContext = runtimeContext;
        Columns = [];
    }

    protected void Init(Func<IEnumerable<dynamic>>? returnQuery = null)
    {
        var blockingCollection = new BlockingCollection<IReadOnlyList<IObjectResolver>>();
        
        DatabaseHelpers.GetDataFromDatabase(
            blockingCollection,
            () => CreateConnection(_runtimeContext),
            () => CreateQueryCommand(_runtimeContext.QueryInformation.FromNode.Method),
            (query, connection) => returnQuery?.Invoke() ?? connection.Query(query),
            _runtimeContext.EndWorkToken);
        
        var columns = new List<ISchemaColumn>();
        
        while (blockingCollection.Count > 0)
        {
            var rows = blockingCollection.Take();
            
            foreach (var row in rows)
            {
                columns.Add(new SchemaColumn((string)row["name"], columns.Count, GetClrType((string)row["type"])));
            }
        }
        
        Columns = columns.ToArray();
    }

    public ISchemaColumn[] Columns { get; private set; }
    
    public SchemaTableMetadata Metadata { get; } = new(typeof(object));

    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(f => f.ColumnName == name);
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }

    protected abstract IDbConnection CreateConnection(RuntimeContext runtimeContext);

    protected abstract string CreateQueryCommand(string name);
    
    protected abstract Type GetClrType(string type);
}