#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Musoq.DataSources.Structured;
using Musoq.Schema;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Json;

internal sealed class JsonStringReuse
{
    private readonly Dictionary<string, int> _names;
    private readonly Dictionary<int, int> _outputs;
    internal RecordStringCache Strings { get; }
    private JsonStringReuse(Dictionary<string, int> names, Dictionary<int, int> outputs)
    { _names = names; _outputs = outputs; Strings = new RecordStringCache(names.Count); }
    internal int PredicateSlot(string name) => _names.TryGetValue(name, out var slot) ? slot : -1;
    internal bool TryReadOutput(int slot, long position, out string value)
    {
        value = null!;
        return _outputs.TryGetValue(slot, out var reuse) && Strings.TryRead(reuse, position, out value);
    }
    internal static JsonStringReuse? Create(QueryRowShape shape, SourcePredicateExpression? predicate)
    {
        if (predicate is null) return null;
        var patterns = new HashSet<string>(StringComparer.Ordinal);
        Add(predicate, patterns);
        if (patterns.Count == 0) return null;
        var fields = shape.Fields.Where(field => field.FieldType == typeof(string) && patterns.Contains(field.Name)).ToArray();
        if (fields.Length == 0) return null;
        var names = fields.Select(field => field.Name).Distinct(StringComparer.Ordinal).Select((name, slot) => (name, slot)).ToDictionary(pair => pair.name, pair => pair.slot, StringComparer.Ordinal);
        return new JsonStringReuse(names, fields.ToDictionary(field => field.Slot, field => names[field.Name]));
    }
    private static void Add(SourcePredicateExpression? predicate, HashSet<string> names)
    {
        if (predicate is SourcePredicateLogical { Operator: SourcePredicateLogicalOperator.And } and)
        { Add(and.Left, names); Add(and.Right, names); }
        else if (predicate is SourcePredicatePatternMatch pattern) names.Add(pattern.Column.Name);
    }
}
