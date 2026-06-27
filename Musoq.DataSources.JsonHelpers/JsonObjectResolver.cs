namespace Musoq.DataSources.JsonHelpers;

/// <summary>
///     Provides dictionary-backed access to JSON object values by column name or index.
/// </summary>
/// <param name="obj">JSON object values keyed by property name.</param>
/// <param name="indexToNameMap">Column index to property name map.</param>
public class JsonObjectResolver(IDictionary<string, object?> obj, IDictionary<int, string> indexToNameMap)
{
    private readonly IDictionary<string, object?> _obj = obj ?? throw new InvalidOperationException();

    /// <summary>
    ///     Gets a value indicating whether the object has the specified column.
    /// </summary>
    /// <param name="name">Column name.</param>
    /// <returns>True when the column exists; otherwise false.</returns>
    public bool HasColumn(string name)
    {
        return _obj.ContainsKey(name);
    }

    /// <summary>
    ///     Gets the underlying object contexts.
    /// </summary>
    public object[] Contexts => [_obj];

    /// <summary>
    ///     Gets a value by column name.
    /// </summary>
    /// <param name="name">Column name.</param>
    public object? this[string name] => _obj[name];

    /// <summary>
    ///     Gets a value by column index.
    /// </summary>
    /// <param name="index">Column index.</param>
    public object? this[int index] => _obj[indexToNameMap[index]];
}
