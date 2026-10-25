using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Common;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.RepresentativeTests;

[TestClass]
public sealed class PatternMatcherTests
{
    [TestMethod]
    public void EarlyAndRowStages_EvaluateEachConjunctOnceAndOwnEachEnumeration()
    {
        var earlyReads = 0;
        var lateReads = 0;
        var bindings = new PatternColumnBindings<Row>(("Text", row => { Interlocked.Increment(ref lateReads); return row.Text; }));
        var nativeBindings = new PatternColumnBindings<Row>(("Text", row => { Interlocked.Increment(ref earlyReads); return row.Text; }));
        var pattern = new SourcePredicateRegexMatch(new("Text"), "^a", TimeSpan.FromMilliseconds(19));
        var result = PatternSourcePlan.Plan(new() { Identity = new("test", "s", "s", "rows"), Predicate = pattern }, bindings, SourcePlanResult.RejectAll);
        var executions = new global::System.Collections.Concurrent.ConcurrentBag<NativePatternExecution>();
        var filtered = PatternSourcePlan.Read(RuntimeV2TestContexts.CreateExecutionContext(executionPlan: result.ExecutionPlan), context =>
        {
            var native = NativePatternExecution.Get(context)!;
            executions.Add(native);
            return new NativeRows(native, nativeBindings);
        }, ["Text"]);
        Parallel.For(0, 4, _ => Assert.AreEqual(3, filtered.Chunks.Sum(chunk => chunk.Count)));
        Assert.AreEqual(4, executions.Distinct().Count());
        Assert.AreEqual(16, earlyReads);
        Assert.AreEqual(0, lateReads);
        Assert.AreSame(pattern, result.ExecutionPlan.PredicateApplications.Single().Predicate);
        var fallback = PatternSourcePlan.Read(RuntimeV2TestContexts.CreateExecutionContext(executionPlan: result.ExecutionPlan), _ => new Rows());
        Assert.AreEqual(3, fallback.Chunks.Sum(chunk => chunk.Count));
        Assert.AreEqual(4, lateReads);
    }

    private sealed class NativeRows(NativePatternExecution execution, PatternColumnBindings<Row> bindings) : RowSource<Row>
    {
        public override IEnumerable<IReadOnlyList<Row>> Chunks
        {
            get
            {
                foreach (var row in new[] { new Row("beta"), new Row("alpha"), new Row("amber"), new Row("apple") })
                    if (execution.Matches(row, bindings)) yield return new[] { row };
            }
        }
    }

    [TestMethod]
    public void LikeShapesAndNegation_MatchCoreAcrossCulturesAndUnicode()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            foreach (var culture in new[] { "en-US", "tr-TR", "pl-PL", "" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                foreach (var needle in new[] { "", "a", "k", "s", "I", "i", "İ", "ı", "K", "ſ", "Żółć", "a.b", "a\\b", "a\nb", "😀" })
                foreach (var kind in Enum.GetValues<SourceStringMatchKind>())
                {
                    var pattern = kind switch { SourceStringMatchKind.Prefix => needle + "%", SourceStringMatchKind.Suffix => "%" + needle, SourceStringMatchKind.Contains => "%" + needle + "%", _ => needle };
                    var core = Operators.PrepareLike(pattern);
                    foreach (var input in new string?[] { null, "", "A", "i", "I", "İ", "ı", "K", "k", "ſ", "s", "K", "ŻÓŁĆ", needle, needle + "\n", "x" + needle, needle + "x", "x" + needle + "y" })
                    foreach (var negated in new[] { false, true })
                    {
                        var predicate = new SourcePredicateStringMatch(new SourceColumnRef("Text"), kind, pattern, needle, isNegated: negated);
                        Assert.AreEqual(Operators.LikePrepared(input, core) != negated, ProviderRegex.Match(predicate, input), $"{culture}, {kind}, needle={needle}, input={input}, negated={negated}");
                    }
                }
            }
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [TestMethod]
    public void ExplicitBindings_UseSameColumnsForCapabilitiesAndFiltering()
    {
        var bindings = new PatternColumnBindings<Row>(("Text", row => row.Text));
        var capabilities = bindings.Capabilities(new[] { new SchemaColumn("Text", 0, typeof(string)), new SchemaColumn("Secret", 1, typeof(string)), new SchemaColumn("Id", 2, typeof(int)) });
        Assert.AreEqual(1, capabilities.StringMatches.Count);
        Assert.AreEqual(1, capabilities.RegexMatches.Count);
        var predicate = new SourcePredicateStringMatch(new SourceColumnRef("Text"), SourceStringMatchKind.Contains, "%a%", "a");
        Assert.IsTrue(bindings.Supports(predicate));
        Assert.IsFalse(bindings.Contains("Secret"));
        Assert.IsFalse(bindings.Contains("Other.Text"));
        Assert.AreEqual("alpha", bindings.Read(new Row("alpha"), "Text"));
    }

    [TestMethod]
    public void FilteringAndPaging_DisposeTheUnderlyingStreamOnEarlyTake()
    {
        var bindings = new PatternColumnBindings<Row>(("Text", row => row.Text));
        var pattern = new SourcePredicateStringMatch(new SourceColumnRef("Text"), SourceStringMatchKind.Prefix, "a%", "a");
        var request = new SourcePlanRequest { Identity = new SourceIdentity("test", "t", "t", "rows"), Predicate = pattern, Skip = 1, Take = 1 };
        var result = PatternSourcePlan.Plan(request, bindings, SourcePlanResult.RejectAll);
        Assert.IsNull(result.ResidualPredicate);
        var source = new Rows();
        var filtered = PatternSourcePlan.Read(RuntimeV2TestContexts.CreateExecutionContext(executionPlan: result.ExecutionPlan), _ => source);
        CollectionAssert.AreEqual(new[] { "amber" }, filtered.Chunks.SelectMany(chunk => chunk).Select(row => row.Text).ToArray());
        Assert.IsTrue(source.Disposed);
    }

    [TestMethod]
    public void UnsupportedPatternColumn_RemainsResidualAndPreventsPaging()
    {
        var bindings = new PatternColumnBindings<Row>(("Text", row => row.Text));
        var pattern = new SourcePredicateRegexMatch(new SourceColumnRef("Secret"), "a", TimeSpan.FromMilliseconds(17));
        var request = new SourcePlanRequest { Identity = new SourceIdentity("test", "t", "t", "rows"), Predicate = pattern, Take = 1 };
        var result = PatternSourcePlan.Plan(request, bindings, SourcePlanResult.RejectAll);
        Assert.AreSame(pattern, result.ResidualPredicate);
        Assert.IsNull(result.AcceptedTake);
        Assert.IsNull(result.ExecutionPlan.AcceptedTake);
    }

    [TestMethod]
    public void PatternUnderOr_IsResidualEvenWhenLegacyPlannerAcceptsEverything()
    {
        var bindings = new PatternColumnBindings<Row>(("Text", row => row.Text));
        var pattern = new SourcePredicateStringMatch(new("Text"), SourceStringMatchKind.Prefix, "a%", "a");
        var predicate = new SourcePredicateLogical(SourcePredicateLogicalOperator.Or, pattern, pattern);
        var result = PatternSourcePlan.Plan(new() { Identity = new("test", "t", "t", "rows"), Predicate = predicate, Take = 1 }, bindings,
            request => { Assert.IsNull(request.Predicate); return SourcePlanResult.RejectAll(request); });
        Assert.AreSame(predicate, result.ResidualPredicate);
        Assert.IsNull(result.AcceptedTake);
        Assert.AreEqual(0, result.ExecutionPlan.PredicateApplications.Count);
    }

    [TestMethod]
    public void LikeMatchers_SharedPlanFollowsOverlappingExecutionCultures()
    {
        var predicate = new SourcePredicateStringMatch(new("Text"), SourceStringMatchKind.Exact, "i", "i");
        Parallel.For(0, 8, index =>
        {
            var original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(index % 2 == 0 ? "en-US" : "tr-TR");
                for (var repeat = 0; repeat < 8; repeat++)
                    Assert.AreEqual(Operators.LikeDynamic("I", "i"), ProviderRegex.Match(predicate, "I"));
            }
            finally { CultureInfo.CurrentCulture = original; }
        });
    }

    [TestMethod]
    public void WildcardInsideClassifiedNeedle_IsSafelyResidual()
    {
        var predicate = new SourcePredicateStringMatch(new("Text"), SourceStringMatchKind.Exact, "a_b", "a_b");
        var bindings = new PatternColumnBindings<Row>(("Text", row => row.Text));
        Assert.IsFalse(bindings.Supports(predicate));
        var plan = PatternSourcePlan.Plan(new() { Identity = new("test", "s", "s", "rows"), Predicate = predicate, Take = 1 }, bindings, SourcePlanResult.RejectAll);
        Assert.AreSame(predicate, plan.ResidualPredicate);
        Assert.IsNull(plan.AcceptedTake);
    }

    private sealed record Row(string? Text);
    private sealed class Rows : RowSource<Row>
    {
        internal bool Disposed;
        public override IEnumerable<IReadOnlyList<Row>> Chunks => Read();
        private IEnumerable<IReadOnlyList<Row>> Read()
        {
            try { yield return new[] { new Row("beta"), new Row("alpha"), new Row("amber"), new Row("apple") }; }
            finally { Disposed = true; }
        }
    }
}
