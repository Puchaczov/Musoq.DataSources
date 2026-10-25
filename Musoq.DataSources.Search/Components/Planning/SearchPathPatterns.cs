#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Musoq.DataSources.Common;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Search.Components.Planning;

internal sealed class SearchPathPatterns
{
    private static readonly PatternColumnBindings<string> Paths = new(("Path", static path => path));
    private readonly SourcePredicatePatternMatch[] _patterns;
    internal NativePatternExecution Execution { get; }
    internal bool Enabled => _patterns.Length != 0;
    internal SearchPathPatterns(SourceExecutionContext context, bool enabled = true)
    {
        var patterns = new List<SourcePredicatePatternMatch>();
        if (enabled) Partition(context.Plan.AcceptedPredicate, patterns);
        _patterns = patterns.ToArray();
        Execution = new NativePatternExecution(_patterns);
    }
    internal bool Matches(string path) => Execution.Matches(path, Paths);
    internal string Fingerprint(string scope)
    {
        if (!Enabled) return scope;
        var value = new StringBuilder(scope).Append('|').Append(CultureInfo.CurrentCulture.Name);
        foreach (var pattern in _patterns)
        {
            var text = pattern switch
            {
                SourcePredicateRegexMatch regex => $"regex:{regex.MatchTimeout.Ticks}:{regex.Pattern}",
                SourcePredicateStringMatch like => $"like:{like.Kind}:{like.Needle}",
                _ => throw new InvalidOperationException("Unsupported path pattern.")
            };
            value.Append('|').Append(pattern.IsNegated).Append(':').Append(text.Length).Append(':').Append(text);
        }
        return "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.ToString()))).ToLowerInvariant();
    }
    internal static SourcePredicateExpression? Remaining(string source, SourcePredicateExpression? predicate)
        => source is "matches" or "many" or "lines" or "files" or "bytes" ? Partition(predicate, null) : predicate;
    private static SourcePredicateExpression? Partition(SourcePredicateExpression? predicate, List<SourcePredicatePatternMatch>? early)
    {
        if (predicate is SourcePredicateLogical { Operator: SourcePredicateLogicalOperator.And } and)
            return PatternSourcePlan.Combine(Partition(and.Left, early), Partition(and.Right, early));
        if (predicate is SourcePredicatePatternMatch pattern && Paths.Supports(pattern))
        { early?.Add(pattern); return null; }
        return predicate;
    }
}

