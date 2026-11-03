#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Linq;
using System.Linq.Expressions;
using Musoq.Schema.DataSources;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Musoq.Schema;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Common;

/// <summary>Core-equivalent, plan-owned LIKE and constant RLIKE evaluation.</summary>
internal static class ProviderRegex
{
    // Weak keys keep compiled matchers alive only while their predicate plans are alive.
    private static readonly ConditionalWeakTable<SourcePredicatePatternMatch, Matcher> Matchers = new();

    internal static bool Supports(SourcePredicateRegexMatch match, Func<SourcePredicateExpression, bool> acceptsComparison)
    {
        if (match.Semantics != SourceRegexSemantics.MusoqRLike || !acceptsComparison(Probe(match.Column)))
            return false;
        _ = Prepare(match);
        return true;
    }

    internal static SourcePredicateCapabilities Capabilities(IEnumerable<ISchemaColumn> columns,
        Func<SourcePredicateExpression, bool> acceptsComparison)
    {
        var matches = new List<SourceRegexMatchCapability>();
        foreach (var column in columns)
            if (column.ColumnType == typeof(string) && acceptsComparison(Probe(new SourceColumnRef(column.ColumnName))))
                matches.Add(new SourceRegexMatchCapability(new SourceColumnRef(column.ColumnName), supportsNegation: true));
        return new SourcePredicateCapabilities { RegexMatches = matches };
    }

    internal static SourcePredicateCapabilities PatternCapabilities(IEnumerable<ISchemaColumn> columns, Func<string, bool> eligible)
    {
        var names = columns.Where(column => column.ColumnType == typeof(string) && eligible(column.ColumnName)).Select(column => column.ColumnName).ToArray();
        return new SourcePredicateCapabilities
        {
            RegexMatches = names.Select(name => new SourceRegexMatchCapability(new SourceColumnRef(name), supportsNegation: true)).ToArray(),
            StringMatches = names.Select(name => new SourceStringMatchCapability(new SourceColumnRef(name), SourceStringMatchOperations.All, supportsNegation: true)).ToArray()
        };
    }

    internal static string ColumnName(string name) => name[(name.LastIndexOf('.') + 1)..];

    private static SourcePredicateComparison Probe(SourceColumnRef column) => new(
        SourcePredicateComparisonOperator.Equal, new SourcePredicateColumn(column), new SourcePredicateLiteral(string.Empty));

    // The LIKE source contract has no per-predicate timeout; this is the pinned
    // 17.0.14 Core default. RLIKE always uses the budget supplied in the predicate.
    internal static readonly TimeSpan LikeTimeout = TimeSpan.FromSeconds(2);

    internal static bool IsSupportedPattern(SourcePredicatePatternMatch match) => match switch
    {
        SourcePredicateRegexMatch regex => regex.Semantics == SourceRegexSemantics.MusoqRLike,
        SourcePredicateStringMatch like => like.Comparison == SourceStringComparison.LikeIgnoreCase && Enum.IsDefined(like.Kind) && like.Needle.IndexOf('%') < 0 && like.Needle.IndexOf('_') < 0,
        _ => false
    };

    internal static Matcher Prepare(SourcePredicatePatternMatch match) => Matchers.GetValue(match,
        static predicate => predicate switch
        {
            SourcePredicateRegexMatch regex => new Matcher(regex.Pattern, regex.MatchTimeout),
            SourcePredicateStringMatch like when IsSupportedPattern(like) => new Matcher(LikePattern(like), LikeTimeout, true),
            _ => throw new NotSupportedException("Unsupported source pattern semantics.")
        });

    private static string LikePattern(SourcePredicateStringMatch match)
    {
        var literal = Regex.Escape(match.Needle);
        return match.Kind switch
        {
            SourceStringMatchKind.Exact => @"\A" + literal + @"\z",
            SourceStringMatchKind.Prefix => @"\A" + literal,
            SourceStringMatchKind.Suffix => literal + @"\z",
            SourceStringMatchKind.Contains => literal,
            _ => throw new NotSupportedException("Unsupported LIKE shape.")
        };
    }

    internal static bool? Match(SourcePredicatePatternMatch match, string? value) => Evaluate(Prepare(match), match.IsNegated, value);

    internal static bool? Evaluate(Matcher matcher, bool negated, string? value)
    {
        // Musoq RLIKE is a Boolean predicate: null is false before negation.
        if (value is null) return negated;
        // Preserve the public typed timeout and its finite budget. Core alpha.2 maps
        // provider regex timeouts to MQ7017 at its datasource lifecycle boundary.
        return matcher.IsMatch(value) != negated;
    }

    internal sealed class Matcher
    {
        private sealed record Entry(string Culture, Regex Regex);
        private readonly string _pattern;
        private readonly TimeSpan _timeout;
        private readonly bool _like;
        private Entry _entry;

        internal Matcher(string pattern, TimeSpan timeout, bool like = false)
        {
            _pattern = pattern;
            _timeout = timeout;
            _like = like;
            _entry = Create();
        }

        // Keep one immutable front entry per plan. Concurrent executions can use
        // different cultures without sharing rows or an unbounded culture cache.
        internal bool IsMatch(string value)
        {
            var entry = Volatile.Read(ref _entry);
            if (entry.Culture != CultureInfo.CurrentCulture.Name)
            {
                entry = Create();
                Volatile.Write(ref _entry, entry);
            }
            return entry.Regex.IsMatch(value);
        }

        private Entry Create()
        {
            Regex regex;
            var options = _like ? RegexOptions.IgnoreCase | RegexOptions.Singleline : RegexOptions.None;
            try { regex = new Regex(_pattern, options | RegexOptions.NonBacktracking, _timeout); }
            catch (NotSupportedException) { regex = new Regex(_pattern, options | RegexOptions.Compiled, _timeout); }
            return new Entry(CultureInfo.CurrentCulture.Name, regex);
        }
    }
}

/// <summary>Typed, explicitly reviewed row getters; never discovers members at runtime.</summary>
internal sealed class PatternColumnBindings<TRow> : IPatternColumnBindings
{
    private readonly IReadOnlyDictionary<string, Func<TRow, string?>> _columns;
    internal PatternColumnBindings(params (string Name, Func<TRow, string?> Read)[] columns)
        => _columns = columns.ToDictionary(column => column.Name, column => column.Read, StringComparer.OrdinalIgnoreCase);
    public IReadOnlyCollection<string> Names => _columns.Keys.ToArray();
    public bool Contains(string name) => _columns.ContainsKey(name);
    public bool Supports(SourcePredicatePatternMatch match) => Contains(match.Column.Name) && ProviderRegex.IsSupportedPattern(match);
    internal string? Read(TRow row, string name) => _columns[name](row);
    internal Expression Field(ParameterExpression row, string name) => Expression.Invoke(Expression.Constant(_columns[name]), row);
    public SourcePredicateCapabilities Capabilities(IEnumerable<ISchemaColumn> columns)
        => ProviderRegex.PatternCapabilities(columns, Contains);
    public RowSource<T> Filter<T>(RowSource<T> source, SourceExecutionContext context, IReadOnlyList<SourcePredicatePatternMatch> patterns)
    {
        if (typeof(T) != typeof(TRow)) throw new InvalidOperationException("Pattern bindings do not match the concrete source row type.");
        return (RowSource<T>)(object)new PatternFilteringRowSource<TRow>((RowSource<TRow>)(object)source, this, context, patterns);
    }
}

internal interface IPatternColumnBindings
{
    IReadOnlyCollection<string> Names { get; }
    bool Contains(string name);
    bool Supports(SourcePredicatePatternMatch match);
    SourcePredicateCapabilities Capabilities(IEnumerable<ISchemaColumn> columns);
    RowSource<T> Filter<T>(RowSource<T> source, SourceExecutionContext context, IReadOnlyList<SourcePredicatePatternMatch> patterns);
}

/// <summary>Composes provider comparison plans with source-row pattern filtering.</summary>
internal static class PatternSourcePlan
{
    private const string PropertyName = "SourceRowPatternPlan";
    private sealed record State(IPatternColumnBindings Bindings, IReadOnlyList<SourcePredicatePatternMatch> Patterns, SourceExecutionPlan InnerPlan);

    internal static SourcePlanResult Plan(SourcePlanRequest request, IPatternColumnBindings? bindings,
        Func<SourcePlanRequest, SourcePlanResult> plan)
    {
        var patterns = new List<SourcePredicatePatternMatch>();
        var blocked = new List<SourcePredicateExpression>();
        var remainder = Extract(request.Predicate, patterns, blocked);
        if (patterns.Count == 0 && blocked.Count == 0) return plan(request);
        var accepted = patterns.Where(pattern => bindings?.Supports(pattern) == true).ToArray();
        var rejected = patterns.Where(pattern => bindings?.Supports(pattern) != true).ToArray();
        var columns = request.RequiredColumns.Concat(accepted.Select(pattern => pattern.Column))
            .DistinctBy(column => column.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        var inner = plan(request with { Predicate = remainder, Skip = null, Take = null, RequiredColumns = columns });
        var predicate = Combine(inner.AcceptedPredicate, Join(accepted));
        var residual = Combine(inner.ResidualPredicate, Combine(Join(rejected), Join(blocked)));
        var slice = residual is null && inner.ResidualOrderBy.Count == 0;
        var properties = new Dictionary<string, object?>(inner.ExecutionPlan.Properties);
        if (accepted.Length > 0) properties[PropertyName] = new State(bindings!, accepted, inner.ExecutionPlan);
        var acceptedColumns = inner.AcceptedColumns.Where(column => request.RequiredColumns.Any(requested => string.Equals(requested.Name, column.Name, StringComparison.OrdinalIgnoreCase))).ToArray();
        return inner with
        {
            AcceptedColumns = acceptedColumns,
            AcceptedPredicate = predicate, ResidualPredicate = residual,
            AcceptedSkip = slice ? request.Skip : null, ResidualSkip = slice ? null : request.Skip,
            AcceptedTake = slice ? request.Take : null, ResidualTake = slice ? null : request.Take,
            Cardinality = CardinalityEstimate.Unknown("Pattern filtering changes the source row count."),
            ExecutionPlan = inner.ExecutionPlan with
            {
                AcceptedColumns = acceptedColumns,
                AcceptedPredicate = predicate, PredicateApplications = SourcePredicateApplication.ForRowFiltering(predicate),
                AcceptedSkip = slice ? request.Skip : null, AcceptedTake = slice ? request.Take : null, Properties = properties
            }
        };
    }

    internal static bool HasPatterns(SourceExecutionContext context)
        => context.Plan.Properties.TryGetValue(PropertyName, out var value) && value is State;

    internal static RowSource<T> Read<T>(SourceExecutionContext context, Func<SourceExecutionContext, RowSource<T>> read, IReadOnlyCollection<string>? earlyColumns = null, bool nativeOwnsWindow = false)
        => HasPatterns(context) ? new PatternExecutionRowSource<T>(() => ReadExecution(context, read, earlyColumns, nativeOwnsWindow)) : read(context);

    private static RowSource<T> ReadExecution<T>(SourceExecutionContext context, Func<SourceExecutionContext, RowSource<T>> read, IReadOnlyCollection<string>? earlyColumns, bool nativeOwnsWindow)
    {
        if (!context.Plan.Properties.TryGetValue(PropertyName, out var value) || value is not State state) return read(context);
        string? sourceName = null;
        var early = state.Patterns.Where(pattern => earlyColumns?.Contains(pattern.Column.Name, StringComparer.OrdinalIgnoreCase) == true).ToArray();
        var native = new NativePatternExecution(early);
        var properties = new Dictionary<string, object?>(state.InnerPlan.Properties) { [NativePatternExecution.PropertyName] = native };
        var innerPlan = state.InnerPlan with { Properties = properties };
        var remaining = state.Patterns.Except(early).ToArray();
        var ownsWindow = nativeOwnsWindow && remaining.Length == 0 && context.Plan.AcceptedTake != 0;
        if (ownsWindow) innerPlan = innerPlan with { AcceptedSkip = context.Plan.AcceptedSkip, AcceptedTake = context.Plan.AcceptedTake };
        var rowContext = ownsWindow ? new SourceExecutionContext(context.QueryId,
            context.Plan with { AcceptedSkip = null, AcceptedTake = null }, context.EndWorkToken,
            context.AllColumns, context.SourceRuntimeSettings, context.Logger, sourceDiagnostics: context.Diagnostics)
            { WarningReporter = context.WarningReporter } : context;
        var innerContext = new SourceExecutionContext(context.QueryId, innerPlan, context.EndWorkToken,
            context.AllColumns, context.SourceRuntimeSettings, context.Logger, (_, args) =>
            {
                if (args.Phase == DataSourcePhase.Begin) sourceName = args.DataSourceName;
                if (args.Phase != DataSourcePhase.End) Forward(context, args);
            }, context.Diagnostics) { WarningReporter = context.WarningReporter };
        return new PatternProgressRowSource<T>(state.Bindings.Filter(read(innerContext), rowContext, remaining), context, () => sourceName, () => native.Report(context));
    }

    private static void Forward(SourceExecutionContext context, DataSourceEventArgs args)
    {
        switch (args.Phase)
        {
            case DataSourcePhase.Begin: context.ReportDataSourceBegin(args.DataSourceName); break;
            case DataSourcePhase.RowsKnown: context.ReportDataSourceRowsKnown(args.DataSourceName, args.TotalRows ?? 0); break;
            case DataSourcePhase.RowsRead: context.ReportDataSourceRowsRead(args.DataSourceName, args.RowsProcessed ?? 0, args.TotalRows); break;
            case DataSourcePhase.End: context.ReportDataSourceEnd(args.DataSourceName, args.RowsProcessed); break;
        }
    }

    private static SourcePredicateExpression? Extract(SourcePredicateExpression? expression, List<SourcePredicatePatternMatch> patterns, List<SourcePredicateExpression> blocked)
    {
        if (expression is SourcePredicateLogical { Operator: SourcePredicateLogicalOperator.And } logical)
            return Combine(Extract(logical.Left, patterns, blocked), Extract(logical.Right, patterns, blocked));
        if (expression is SourcePredicatePatternMatch pattern) { patterns.Add(pattern); return null; }
        if (ContainsPattern(expression)) { blocked.Add(expression!); return null; }
        return expression;
    }
    internal static bool ContainsPattern(SourcePredicateExpression? expression) => expression switch
    {
        SourcePredicatePatternMatch => true,
        SourcePredicateLogical logical => ContainsPattern(logical.Left) || ContainsPattern(logical.Right),
        _ => false
    };
    private static SourcePredicateExpression? Join(IEnumerable<SourcePredicateExpression> patterns)
    {
        SourcePredicateExpression? result = null;
        foreach (var pattern in patterns) result = Combine(result, pattern);
        return result;
    }
    internal static SourcePredicateExpression? Combine(SourcePredicateExpression? left, SourcePredicateExpression? right)
        => left is null ? right : right is null ? left : new SourcePredicateLogical(SourcePredicateLogicalOperator.And, left, right);
}

internal sealed class PatternFilteringRowSource<T>(RowSource<T> source, PatternColumnBindings<T> bindings,
    SourceExecutionContext context, IReadOnlyList<SourcePredicatePatternMatch> patterns) : RowSource<T>
{
    // Pull-based iteration disposes the underlying enumerator on early TAKE or failure.
    // Each yielded chunk owns its buffer for as long as a consumer can observe it.
    public override IEnumerable<IReadOnlyList<T>> Chunks => Read();
    private IEnumerable<IReadOnlyList<T>> Read()
    {
        var tests = patterns.Select(pattern => (pattern.Column.Name, pattern.IsNegated, Matcher: ProviderRegex.Prepare(pattern))).ToArray();
        var skip = context.Plan.AcceptedSkip ?? 0;
        var take = context.Plan.AcceptedTake;
        var emitted = 0L;
        var buffer = new List<T>(RowChunking.DefaultChunkSize);
        if (take == 0) yield break;
        foreach (var chunk in source.Chunks)
            foreach (var row in chunk)
            {
                context.EndWorkToken.ThrowIfCancellationRequested();
                var matches = true;
                foreach (var test in tests)
                    if (ProviderRegex.Evaluate(test.Matcher, test.IsNegated, bindings.Read(row, test.Name)) != true) { matches = false; break; }
                if (!matches) continue;
                if (skip > 0) { skip--; continue; }
                buffer.Add(row); emitted++;
                if (buffer.Count == RowChunking.DefaultChunkSize) { yield return buffer; buffer = new List<T>(RowChunking.DefaultChunkSize); }
                if (take.HasValue && emitted >= take.Value) { if (buffer.Count > 0) yield return buffer; yield break; }
            }
        if (buffer.Count > 0) yield return buffer;
    }
}

internal sealed class PatternProgressRowSource<T>(RowSource<T> source, SourceExecutionContext context, Func<string?> sourceName, Action? completed = null) : RowSource<T>
{
    public override IEnumerable<IReadOnlyList<T>> Chunks => Read();
    private IEnumerable<IReadOnlyList<T>> Read()
    {
        long emitted = 0;
        try
        {
            foreach (var chunk in source.Chunks) { emitted += chunk.Count; yield return chunk; }
        }
        finally
        {
            completed?.Invoke();
            if (sourceName() is { } name) context.ReportDataSourceEnd(name, emitted);
        }
    }
}

/// <summary>One execution owns native predicate evaluation and bounded work counters.</summary>
internal sealed class NativePatternExecution
{
    internal const string PropertyName = "NativePatternExecution";
    private readonly (string Name, bool Negated, ProviderRegex.Matcher Matcher)[] _tests;
    private long _candidates, _rejected, _rows, _opens, _decoded, _pages, _rich;
    internal NativePatternExecution(IReadOnlyList<SourcePredicatePatternMatch> patterns)
        => _tests = patterns.Select(pattern => (pattern.Column.Name, pattern.IsNegated, ProviderRegex.Prepare(pattern))).ToArray();
    internal long Candidates => Interlocked.Read(ref _candidates);
    internal long Rejected => Interlocked.Read(ref _rejected);
    internal long DecodedStrings => Interlocked.Read(ref _decoded);
    internal long RichFieldLoads => Interlocked.Read(ref _rich);
    internal IReadOnlyCollection<string> Names => _tests.Select(test => test.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    internal bool Requires(string name) => _tests.Any(test => string.Equals(test.Name, name, StringComparison.OrdinalIgnoreCase));
    internal bool HasPredicates => _tests.Length != 0;
    internal static NativePatternExecution? Get(SourceExecutionContext context) => Get(context.Plan);
    internal static NativePatternExecution? Get(SourceExecutionPlan plan)
        => plan.Properties.TryGetValue(PropertyName, out var value) ? value as NativePatternExecution : null;
    internal bool Matches<T>(T value, PatternColumnBindings<T> bindings)
    {
        Interlocked.Increment(ref _candidates);
        foreach (var test in _tests)
            if (ProviderRegex.Evaluate(test.Matcher, test.Negated, bindings.Read(value, test.Name)) != true)
            { Interlocked.Increment(ref _rejected); return false; }
        return true;
    }
    internal void Constructed(long count = 1) => Interlocked.Add(ref _rows, count);
    internal void ContentOpened(long count = 1) => Interlocked.Add(ref _opens, count);
    internal void RichFieldLoaded() => Interlocked.Increment(ref _rich);
    internal void Decoded(long count = 1) => Interlocked.Add(ref _decoded, count);
    internal void PageRead() => Interlocked.Increment(ref _pages);
    internal void Report(SourceExecutionContext context)
    {
        context.Diagnostics.AddMetric("Pattern.Candidates", _candidates);
        context.Diagnostics.AddMetric("Pattern.RejectedCandidates", _rejected);
        context.Diagnostics.AddMetric("Pattern.ConstructedRows", _rows);
        context.Diagnostics.AddMetric("Pattern.ContentOpens", _opens);
        context.Diagnostics.AddMetric("Pattern.DecodedStrings", _decoded);
        context.Diagnostics.AddMetric("Pattern.ApiPages", _pages);
        context.Diagnostics.AddMetric("Pattern.RichFieldLoads", _rich);
    }
}

internal sealed class PatternExecutionRowSource<T>(Func<RowSource<T>> create) : RowSource<T>
{
    public override IEnumerable<IReadOnlyList<T>> Chunks => Read();
    private IEnumerable<IReadOnlyList<T>> Read()
    {
        foreach (var chunk in create().Chunks) yield return chunk;
    }
}

internal interface IRawPatternPage { int RawCount { get; } }
internal sealed class PatternPage<T>(IReadOnlyList<T> rows, int rawCount) : IReadOnlyList<T>, IRawPatternPage
{
    public int RawCount => rawCount;
    public int Count => rows.Count;
    public T this[int index] => rows[index];
    public IEnumerator<T> GetEnumerator() => rows.GetEnumerator();
    global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
internal static class PatternPage
{
    internal static int RawCount<T>(IReadOnlyList<T> rows) => rows is IRawPatternPage page ? page.RawCount : rows.Count;
    internal static IReadOnlyList<TRow> Map<TValue, TRow>(IEnumerable<TValue> values, NativePatternExecution? native,
        PatternColumnBindings<TValue> bindings, Func<TValue, TRow> create, CancellationToken token)
    {
        if (native is null) return values.Select(create).ToList();
        native.PageRead();
        var rows = new List<TRow>(values.TryGetNonEnumeratedCount(out var capacity) ? capacity : 0);
        var count = 0;
        foreach (var value in values)
        {
            token.ThrowIfCancellationRequested();
            count++;
            if (!native.Matches(value, bindings)) continue;
            rows.Add(create(value));
            native.Constructed();
        }
        return new PatternPage<TRow>(rows, count);
    }
}
