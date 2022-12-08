using Musoq.Schema;

namespace Musoq.DataSources.Time
{
    public class TimeSchemaProvider : ISchemaProvider
    {
        public ISchema GetSchema(string schema)
        {
            return new TimeSchema();
        }
    }
}