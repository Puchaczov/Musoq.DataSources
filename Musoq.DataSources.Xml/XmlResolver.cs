using System;
using System.Collections.Generic;
using System.Diagnostics;
using Musoq.Schema.DataSources;

namespace Musoq.Schema.Xml;

#if DEBUG
[DebuggerDisplay("{" + nameof(DebugString) + "()}")]
#endif
internal class XmlResolver<T> : IObjectResolver
{
    private readonly T _entity;
    private readonly IDictionary<int, Func<T, object>> _indexToObjectAccessMap;
    private readonly IDictionary<string, int> _nameToIndexMap;

    public XmlResolver(T entity, IDictionary<string, int> nameToIndexMap,
        IDictionary<int, Func<T, object>> indexToObjectAccessMap)
    {
        _entity = entity;
        _nameToIndexMap = nameToIndexMap;
        _indexToObjectAccessMap = indexToObjectAccessMap;
    }

    public object[] Contexts => new object[] { _entity };

    object IObjectResolver.this[string name]
    {
        get
        {
            if (!_nameToIndexMap.ContainsKey(name))
            {
                return null;
            }

            return _indexToObjectAccessMap[_nameToIndexMap[name]](_entity);
        }
    }

    object IObjectResolver.this[int index]
        => _indexToObjectAccessMap[index](_entity);

    public bool HasColumn(string name)
    {
        return _nameToIndexMap.ContainsKey(name);
    }

#if DEBUG
    public string DebugString()
    {
        return $"{_entity.ToString()}";
    }
#endif
}