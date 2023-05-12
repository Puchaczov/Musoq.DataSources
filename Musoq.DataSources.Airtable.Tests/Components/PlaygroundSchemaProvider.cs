using Musoq.Schema;

namespace Musoq.DataSources.Airtable.Tests.Components;

public class PlaygroundSchemaProvider : ISchemaProvider
{
    public ISchema GetSchema(string schema)
    {
        return new AirtableSchema();
    }
}