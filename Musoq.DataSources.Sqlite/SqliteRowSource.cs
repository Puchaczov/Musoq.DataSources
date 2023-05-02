using System.Data.Common;
using System.Text;
using Microsoft.Data.Sqlite;
using Musoq.DataSources.Databases;
using Musoq.DataSources.Sqlite.Visitors;
using Musoq.Schema;

namespace Musoq.DataSources.Sqlite;

internal class SqliteRowSource : DatabaseRowSource
{
    private readonly RuntimeContext _runtimeContext;
    
    public SqliteRowSource(RuntimeContext runtimeContext) 
        : base(runtimeContext, null)
    {
        _runtimeContext = runtimeContext;
    }

    protected override DbConnection CreateConnection(IReadOnlyDictionary<string, string> environmentVariables)
    {
        return new SqliteConnection(_runtimeContext.EnvironmentVariables["SQLITE_CONNECTION_STRING"]);
    }

    protected override string CreateQueryCommand()
    {
        var queryBuilder = new StringBuilder();

        queryBuilder.Append("SELECT");
        queryBuilder.Append(string.Join(",", _runtimeContext.QueryInformation.Columns.Select(f => $" {f.ColumnName}")));
        queryBuilder.Append(" FROM ");
        queryBuilder.Append(_runtimeContext.QueryInformation.FromNode.Method);
        queryBuilder.Append(" WHERE ");
        
        var visitor = new ToStringWhereQueryPartVisitor();
        var traverser = new ToStringWhereQueryPartTraverseVisitor(visitor);
        
        _runtimeContext.QueryInformation.WhereNode.Accept(traverser);
        
        queryBuilder.Append(visitor.StringifiedWherePart);
        
        return queryBuilder.ToString();
    }
}