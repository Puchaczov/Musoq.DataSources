using Musoq.Schema;

namespace Musoq.DataSources.Archives;

/// <summary>
/// Provides the requested schema
/// </summary>
public class ArchivesSchemaProvider : ISchemaProvider
{
    /// <summary>
    /// Get schema based on provided name
    /// </summary>
    /// <param name="schema">Schema name</param>
    /// <returns>Requested schema</returns>
    public ISchema GetSchema(string schema)
    {
        return new ArchivesSchema();
    }
}