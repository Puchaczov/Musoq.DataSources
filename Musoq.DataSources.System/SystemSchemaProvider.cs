using Musoq.Schema;

namespace Musoq.DataSources.System
{
    public class SystemSchemaProvider : ISchemaProvider
    {
        public ISchema GetSchema(string schema)
        {
            return new SystemSchema();
        }
    }
}