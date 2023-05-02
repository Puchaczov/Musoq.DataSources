using Musoq.Schema;

namespace Musoq.DataSources.Postgres;

internal class PostgresSchemaProvider : ISchemaProvider
{
    public ISchema GetSchema(string schema)
    {
        return new PostgresSchema();
    }
}