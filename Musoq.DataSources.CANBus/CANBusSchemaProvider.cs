using Musoq.Schema;

namespace Musoq.DataSources.CANBus;

/// <summary>
///     Provides the schema for CAN bus data.
/// </summary>
public class CANBusSchemaProvider : ISchemaProvider
{
    /// <summary>
    ///     Gets the schema to work with CAN bus data.
    /// </summary>
    /// <param name="schema">Requested schema</param>
    /// <returns>Requested schema</returns>
    public ISchema GetSchema(string schema)
    {
        return new CANBusSchema();
    }
}