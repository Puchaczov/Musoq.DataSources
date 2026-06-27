namespace Musoq.DataSources.JsonHelpers;

public class JsonObjectResolver(IDictionary<string, object?> obj, IDictionary<int, string> indexToNameMap)
{
    private readonly IDictionary<string, object?> _obj = obj ?? throw new InvalidOperationException();

    public bool HasColumn(string name)
    {
        return _obj.ContainsKey(name);
    }

    public object[] Contexts => [_obj];

    public object? this[string name] => _obj[name];

    public object? this[int index] => _obj[indexToNameMap[index]];
}
