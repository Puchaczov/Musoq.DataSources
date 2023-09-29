using System.Collections.Generic;
using System.Dynamic;

namespace Musoq.Schema.Xml
{
    internal class DynamicElement : DynamicObject
    {
        private readonly Dictionary<string, object> _values;

        public DynamicElement(Dictionary<string, object> values)
        {
            _values = values;
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            // We don't care about the return value...
            _values.TryGetValue(binder.Name, out result);
            return true;
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            _values[binder.Name] = value;
            return true;
        }

        internal void Add(string key, object @object)
        {
            _values.Add(key, @object);
        }
    }
}
