using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Musoq.DataSources.Structured;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Json;

internal sealed class JsonPredicateEvaluator
{
    private static readonly JsonPredicateEvaluator Empty = new([]);
    private readonly JsonPredicateTerm[] _terms;

    private JsonPredicateEvaluator(JsonPredicateTerm[] terms)
    {
        _terms = terms;
    }

    public static JsonPredicateEvaluator Create(
        StructuredSchemaSnapshot snapshot,
        SourcePredicateExpression predicate,
        JsonRowLayout layout)
    {
        if (predicate is null)
            return Empty;

        var terms = new List<JsonPredicateTerm>();
        AddTerms(snapshot, predicate, layout, terms);
        return terms.Count == 0 ? Empty : new JsonPredicateEvaluator(terms.ToArray());
    }

    public bool Matches(ReadOnlySpan<byte> record, JsonReaderOptions options)
    {
        if (_terms.Length == 0)
            return true;

        Span<bool> seen = _terms.Length <= 64
            ? stackalloc bool[_terms.Length]
            : new bool[_terms.Length];
        Span<bool> matched = _terms.Length <= 64
            ? stackalloc bool[_terms.Length]
            : new bool[_terms.Length];
        Span<bool> propertyMatches = _terms.Length <= 64
            ? stackalloc bool[_terms.Length]
            : new bool[_terms.Length];
        var reader = new Utf8JsonReader(record, true, new JsonReaderState(options));
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("A framed JSON row must be an object.");

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;
            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("A JSON object must contain property names and values.");

            var nameHash = reader.ValueIsEscaped ? 0 : JsonPropertyLookup.Hash(reader.ValueSpan);
            propertyMatches.Clear();
            for (var index = 0; index < _terms.Length; index++)
                propertyMatches[index] = _terms[index].NameMatches(nameHash, ref reader);

            if (!reader.Read())
                throw new JsonException("A JSON property has no value.");

            for (var index = 0; index < _terms.Length; index++)
            {
                ref readonly var term = ref _terms[index];
                if (!propertyMatches[index])
                    continue;
                seen[index] = true;
                matched[index] = term.Evaluate(ref reader);
            }

            if (reader.TokenType is JsonTokenType.StartArray or JsonTokenType.StartObject)
                reader.Skip();
        }

        for (var index = 0; index < _terms.Length; index++)
            if (!seen[index] || !matched[index])
                return false;
        return true;
    }

    private static void AddTerms(
        StructuredSchemaSnapshot snapshot,
        SourcePredicateExpression predicate,
        JsonRowLayout layout,
        List<JsonPredicateTerm> terms)
    {
        switch (predicate)
        {
            case SourcePredicateLogical { Operator: SourcePredicateLogicalOperator.And } logical:
                AddTerms(snapshot, logical.Left, layout, terms);
                AddTerms(snapshot, logical.Right, layout, terms);
                return;
            case SourcePredicateComparison comparison when
                JsonSourcePlanner.TryGetComparisonParts(comparison, out var name, out var literal, out var op):
            {
                if (!snapshot.TryGetColumn(name, out var column))
                {
                    var dotIndex = name.LastIndexOf('.');
                    if (dotIndex < 0 || !snapshot.TryGetColumn(name[(dotIndex + 1)..], out column))
                        throw new StructuredUnknownColumnException(name, snapshot.Identity.CanonicalPath);
                    name = column.Name;
                }

                _ = layout.GetSlot(name);
                terms.Add(JsonPredicateTerm.Create(name, column.TypeState.Kind, literal.Value, op));
                return;
            }
            default:
                throw new InvalidOperationException("JSON execution received a predicate it did not accept.");
        }
    }
}

internal abstract class JsonPredicateTerm
{
    private readonly ulong _nameHash;
    private readonly byte[] _nameUtf8;
    protected readonly SourcePredicateComparisonOperator Operator;

    protected JsonPredicateTerm(string name, SourcePredicateComparisonOperator op)
    {
        _nameUtf8 = Encoding.UTF8.GetBytes(name);
        _nameHash = JsonPropertyLookup.Hash(_nameUtf8);
        Operator = op;
    }

    public static JsonPredicateTerm Create(string name, StructuredValueKind type, object literal,
        SourcePredicateComparisonOperator op) => type switch
    {
        StructuredValueKind.Long => new NumericTerm<long, LongReader>(name, op, Convert.ToInt64(literal, CultureInfo.InvariantCulture)),
        StructuredValueKind.Decimal => new NumericTerm<decimal, DecimalReader>(name, op, Convert.ToDecimal(literal, CultureInfo.InvariantCulture)),
        StructuredValueKind.Double => new NumericTerm<double, DoubleReader>(name, op, Convert.ToDouble(literal, CultureInfo.InvariantCulture)),
        StructuredValueKind.Boolean => new NumericTerm<bool, BooleanReader>(name, op, (bool)literal),
        StructuredValueKind.String => new StringTerm(name, op, (string)literal),
        _ => throw new InvalidOperationException($"JSON predicate type '{type}' is not supported.")
    };

    public bool NameMatches(ulong unescapedHash, ref Utf8JsonReader propertyReader)
        => propertyReader.ValueIsEscaped
            ? propertyReader.ValueTextEquals(_nameUtf8)
            : unescapedHash == _nameHash && propertyReader.ValueSpan.SequenceEqual(_nameUtf8);

    public abstract bool Evaluate(ref Utf8JsonReader reader);

    private interface IValueReader<T>
    {
        static abstract T Read(ref Utf8JsonReader reader);
    }

    private readonly struct LongReader : IValueReader<long>
    {
        public static long Read(ref Utf8JsonReader reader) => reader.GetInt64();
    }

    private readonly struct DecimalReader : IValueReader<decimal>
    {
        public static decimal Read(ref Utf8JsonReader reader) => reader.GetDecimal();
    }

    private readonly struct DoubleReader : IValueReader<double>
    {
        public static double Read(ref Utf8JsonReader reader) => reader.GetDouble();
    }

    private readonly struct BooleanReader : IValueReader<bool>
    {
        public static bool Read(ref Utf8JsonReader reader) => reader.GetBoolean();
    }

    private sealed class NumericTerm<T, TReader>(string name, SourcePredicateComparisonOperator op, T literal)
        : JsonPredicateTerm(name, op)
        where T : struct, IComparable<T>
        where TReader : struct, IValueReader<T>
    {
        public override bool Evaluate(ref Utf8JsonReader reader)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return false;
            var comparison = TReader.Read(ref reader).CompareTo(literal);
            return Operator switch
            {
                SourcePredicateComparisonOperator.Equal => comparison == 0,
                SourcePredicateComparisonOperator.NotEqual => comparison != 0,
                SourcePredicateComparisonOperator.GreaterThan => comparison > 0,
                SourcePredicateComparisonOperator.GreaterOrEqual => comparison >= 0,
                SourcePredicateComparisonOperator.LessThan => comparison < 0,
                SourcePredicateComparisonOperator.LessOrEqual => comparison <= 0,
                _ => false
            };
        }
    }

    private sealed class StringTerm : JsonPredicateTerm
    {
        private readonly byte[] _literalUtf8;
        internal StringTerm(string name, SourcePredicateComparisonOperator op, string literal) : base(name, op)
            => _literalUtf8 = Encoding.UTF8.GetBytes(literal);

        public override bool Evaluate(ref Utf8JsonReader reader)
            => reader.TokenType == JsonTokenType.String && (Operator == SourcePredicateComparisonOperator.Equal
                ? reader.ValueTextEquals(_literalUtf8)
                : !reader.ValueTextEquals(_literalUtf8));
    }
}
