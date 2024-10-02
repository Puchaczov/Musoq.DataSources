using Musoq.Schema;

namespace Musoq.DataSources.Roslyn.Tests;

public class RoslynSchemaProvider : ISchemaProvider
{
    public ISchema GetSchema(string schema)
    {
        return new RoslynSchema();
    }
}