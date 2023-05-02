using Musoq.Schema;

namespace Musoq.DataSources.Archives;

public class ArchivesSchemaProvider : ISchemaProvider
{
    public ISchema GetSchema(string schema)
    {
        return new ArchivesSchema();
    }
}