using Musoq.Schema;

namespace Musoq.DataSources.SeparatedValues.Tests.Infrastructure;

internal class CsvSchemaProvider : ISchemaProvider
{
    public ISchema GetSchema(string schema)
    {
        return new SeparatedValuesSchema();
    }
}
