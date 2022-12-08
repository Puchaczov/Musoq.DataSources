using Musoq.Schema;

namespace Musoq.DataSources.SeparatedValues.Tests
{
    internal class CsvSchemaProvider : ISchemaProvider
    {
        public ISchema GetSchema(string schema)
        {
            return new SeparatedValuesSchema();
        }
    }
}