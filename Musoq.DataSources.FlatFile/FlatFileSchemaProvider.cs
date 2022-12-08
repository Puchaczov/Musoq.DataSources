using Musoq.Schema;

namespace Musoq.DataSources.FlatFile
{
    public class FlatFileSchemaProvider : ISchemaProvider
    {
        public ISchema GetSchema(string schema)
        {
            return new FlatFileSchema();
        }
    }
}