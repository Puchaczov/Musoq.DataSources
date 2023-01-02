using Musoq.Schema;

namespace Musoq.DataSources.System
{
    /// <summary>
    /// Provides the requested schema
    /// </summary>
    public class SystemSchemaProvider : ISchemaProvider
    {
        /// <summary>
        /// Get schema based on provided name
        /// </summary>
        /// <param name="schema">Schema name</param>
        /// <returns>Requested schema</returns>
        public ISchema GetSchema(string schema)
        {
            return new SystemSchema();
        }
    }
}