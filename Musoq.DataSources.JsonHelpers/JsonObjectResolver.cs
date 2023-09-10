using Musoq.Schema.DataSources;

namespace Musoq.DataSources.JsonHelpers;

public class JsonObjectResolver : IObjectResolver
{
    private readonly IDictionary<string, object?> _obj;
    private readonly IDictionary<int, string> _indexToNameMap;

    public JsonObjectResolver(IDictionary<string, object?> obj, IDictionary<int, string> indexToNameMap)
    {
        _obj = obj ?? throw new InvalidOperationException();
        _indexToNameMap = indexToNameMap;
    }

    public bool HasColumn(string name) => _obj.ContainsKey(name);

    public object[] Contexts => new object[] { _obj };

    public object? this[string name] => _obj[name];

    public object? this[int index] => _obj[_indexToNameMap[index]];
}