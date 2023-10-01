using System.Collections.Generic;
using System.Dynamic;

namespace Musoq.Schema.Xml
{
    /// <summary>
    /// Represents a dynamic element.
    /// </summary>
    public class DynamicElement : DynamicObject
    {
        internal readonly Dictionary<string, object> Values;

        internal DynamicElement(Dictionary<string, object> values)
        {
            Values = values;
        }

        /// <summary>
        /// Gets the keys of the dynamic element.
        /// </summary>
        public IEnumerable<string> Keys => Values.Keys;

        /// <summary>
        /// Gets the values of the dynamic element.
        /// </summary>
        /// <param name="binder">The binder.</param>
        /// <param name="result">The result.</param>
        /// <returns>True if the value was found, false otherwise.</returns>
        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            // We don't care about the return value...
            Values.TryGetValue(binder.Name, out result);
            return true;
        }

        /// <summary>
        /// Sets the value of the dynamic element.
        /// </summary>
        /// <param name="binder">The binder.</param>
        /// <param name="value">The value.</param>
        /// <returns>True if the value was set, false otherwise.</returns>
        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            Values[binder.Name] = value;
            return true;
        }

        internal void Add(string key, object @object)
        {
            Values.Add(key, @object);
        }
    }
}
