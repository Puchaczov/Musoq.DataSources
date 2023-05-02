using System.Data;
using Musoq.DataSources.Databases;
using Musoq.Schema;
using Npgsql;

namespace Musoq.DataSources.Postgres;

internal class PostgresTable : DatabaseTable
{
    private readonly string _schema;
    
    public PostgresTable(RuntimeContext runtimeContext, string schema, Func<IEnumerable<dynamic>>? returnQuery = null) 
        : base(runtimeContext)
    {
        _schema = schema;
        Init(returnQuery);
    }

    protected override IDbConnection CreateConnection(RuntimeContext runtimeContext)
    {
        return new NpgsqlConnection(runtimeContext.EnvironmentVariables["NPGSQL_CONNECTION_STRING"]);
    }
    
    protected override string CreateQueryCommand(string name)
    {
        return $"SELECT column_name as name, data_type as type FROM information_schema.columns WHERE table_schema = '{_schema}' AND table_name = '{name}'";
    }

    protected override Type GetClrType(string type)
    {
        if (string.IsNullOrEmpty(type))
        {
            throw new ArgumentNullException(nameof(type));
        }

        switch (type.ToLowerInvariant())
        {
            case "bigint":
                return typeof(long);
            case "boolean":
                return typeof(bool);
            case "char":
                return typeof(char);
            case "character":
                return typeof(string);
            case "date":
                return typeof(DateTime);
            case "double precision":
                return typeof(double);
            case "integer":
            case "int":
                return typeof(int);
            case "numeric":
                return typeof(decimal);
            case "real":
                return typeof(float);
            case "smallint":
                return typeof(short);
            case "text":
                return typeof(string);
            case "timestamp":
                return typeof(DateTime);
            case "uuid":
                return typeof(Guid);
            case "varchar":
                return typeof(string);
            // Add more PostgreSQL type to CLR type mappings here as needed.
            default:
                throw new NotSupportedException($"The PostgreSQL type '{type}' is not supported.");
        }
    }
}