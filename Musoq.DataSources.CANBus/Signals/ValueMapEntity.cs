using System.Collections.Generic;

namespace Musoq.DataSources.CANBus.Signals;

/// <summary>
///     Represents a single key value map pair.
/// </summary>
public class ValueMapEntity
{
    private readonly KeyValuePair<int, string> _valueTableMap;

    /// <summary>
    ///     Creates a new instance of <see cref="ValueMapEntity" />.
    /// </summary>
    /// <param name="valueTableMap">The value table key value pair.</param>
    public ValueMapEntity(KeyValuePair<int, string> valueTableMap)
    {
        _valueTableMap = valueTableMap;
    }

    /// <summary>
    ///     Gets the value presented in signal.
    /// </summary>
    public int Value => _valueTableMap.Key;

    /// <summary>
    ///     Gets the name of the value.
    /// </summary>
    public string Name => _valueTableMap.Value;
}