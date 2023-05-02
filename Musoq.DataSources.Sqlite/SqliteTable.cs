using System.Data.Common;
using Microsoft.Data.Sqlite;
using Musoq.DataSources.Databases;
using Musoq.Schema;

namespace Musoq.DataSources.Sqlite;

internal class SqliteTable : DatabaseTable
{
    public SqliteTable(RuntimeContext runtimeContext) 
        : base(runtimeContext)
    {
        Init();
    }

    protected override DbConnection CreateConnection(RuntimeContext runtimeContext)
    {
        return new SqliteConnection(runtimeContext.EnvironmentVariables["SQLITE_CONNECTION_STRING"]);
    }
    
    protected override string CreateQueryCommand(string name)
    {
        return $"PRAGMA table_info('{name}')";
    }

    protected override Type GetClrType(string type)
    {
        return type.ToLowerInvariant() switch
        {
            "null" => typeof(DBNull),
            "integer" => typeof(long),
            "real" => typeof(double),
            "text" => typeof(string),
            "blob" => typeof(byte[]),
            _ => throw new NotSupportedException($"SQLite type '{type}' not supported.")
        };
    }
}