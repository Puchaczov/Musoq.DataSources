using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Airtable;

internal class AirtableObjectResolver : IObjectResolver
{
    private readonly IDictionary<string, object> _obj;
    private readonly IDictionary<int, string> _indexToNameMap;
    private readonly HashSet<string> _columns;

    public AirtableObjectResolver(IDictionary<string, object> obj, IDictionary<int, string> indexToNameMap, HashSet<string> columns)
    {
        _obj = obj ?? throw new InvalidOperationException();
        _indexToNameMap = indexToNameMap;
        _columns = columns;
    }

    public bool HasColumn(string name) => _obj.ContainsKey(name);

    public object[] Contexts => new object[] { _obj };

    public object? this[string name]
    {
        get
        {
            var hasColumn = _columns.Contains(name);

            return hasColumn switch
            {
                true when _obj.TryGetValue(name, out var item) => item,
                true => null,
                _ => throw new InvalidOperationException($"Column {name} does not exist.")
            };
        }
    }

    public object this[int index] => _obj[_indexToNameMap[index]];
}