using Musoq.Schema;

namespace Musoq.DataSources.Search.Tests.Infrastructure;

internal sealed class SearchSchemaProvider : ISchemaProvider
{
    public ISchema GetSchema(string schema)
    {
        return new SearchSchema();
    }
}
