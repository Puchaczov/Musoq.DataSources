using System.Data;
using System.Text;
using Musoq.DataSources.Databases;
using Musoq.DataSources.Postgres.Visitors;
using Musoq.Schema;
using Npgsql;

namespace Musoq.DataSources.Postgres;

internal class PostgresRowSource : DatabaseRowSource
{
    private readonly RuntimeContext _runtimeContext;
    private readonly string _schema;
    
    public PostgresRowSource(RuntimeContext runtimeContext, string schema, Func<IEnumerable<dynamic>>? returnQuery = null) 
        : base(runtimeContext, returnQuery)
    {
        _runtimeContext = runtimeContext;
        _schema = schema;
    }

    protected override IDbConnection CreateConnection()
    {
        return new NpgsqlConnection(_runtimeContext.EnvironmentVariables["NPGSQL_CONNECTION_STRING"]);
    }

    protected override string CreateQueryCommand()
    {
        var queryBuilder = new StringBuilder();

        queryBuilder.Append("SELECT");
        queryBuilder.Append(string.Join(",", _runtimeContext.QueryInformation.Columns.Select(f => $" \"{f.ColumnName}\"")));
        queryBuilder.Append(" FROM ");
        queryBuilder.Append($"{_schema}.\"{_runtimeContext.QueryInformation.FromNode.Method}\"");
        queryBuilder.Append(" WHERE ");
        
        var visitor = new ToStringWhereQueryPartVisitor();
        var traverser = new ToStringWhereQueryPartTraverseVisitor(visitor);
        
        _runtimeContext.QueryInformation.WhereNode.Accept(traverser);
        
        queryBuilder.Append(visitor.StringifiedWherePart);
        
        return queryBuilder.ToString();
    }
}