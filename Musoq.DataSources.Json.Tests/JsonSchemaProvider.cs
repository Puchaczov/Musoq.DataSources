using Musoq.Schema;

namespace Musoq.DataSources.Json.Tests;

internal class JsonSchemaProvider : ISchemaProvider
{
    public ISchema GetSchema(string schema)
    {
        return new JsonSchema();
    }
}