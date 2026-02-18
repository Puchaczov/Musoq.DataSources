using Musoq.Schema.DataSources;

namespace Musoq.DataSources.JsonHelpers;

/// <inheritdoc />
public class JsonObjectResolver(IDictionary<string, object?> obj, IDictionary<int, string> indexToNameMap)
    : IObjectResolver
{
    private readonly IDictionary<string, object?> _obj = obj ?? throw new InvalidOperationException();

    /// <inheritdoc />
    public bool HasColumn(string name)
    {
        return _obj.ContainsKey(name);
    }

    /// <inheritdoc />
    public object[] Contexts => [_obj];

    /// <inheritdoc />
    public object? this[string name] => _obj[name];

    /// <inheritdoc />
    public object? this[int index] => _obj[indexToNameMap[index]];
}