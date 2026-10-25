#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Search.Components.Contracts;
using Musoq.DataSources.Search.Components.Diagnostics;
using Musoq.DataSources.Search.Components.Execution;
using Musoq.DataSources.Search.Components.Testing;
using Musoq.DataSources.Search.Sources;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Search.Tests;

[TestClass]
public class EarlyPatternTests
{
    private string _root = null!;
    [TestInitialize]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "musoq-search-early-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "keep.txt"), "TODO\n");
        File.WriteAllText(Path.Combine(_root, "reject.txt"), new string('X', 5000));
    }
    [TestCleanup]
    public void Cleanup() => Directory.Delete(_root, true);

    [TestMethod]
    public void AllContentSources_ApplyBothPatternFamiliesBeforeObservationAndOpen()
    {
        using var locked = File.Open(Path.Combine(_root, "reject.txt"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var hooks = SearchTestHooks.Install(new()
        {
            BeforeContentOpen = path => Assert.AreEqual("keep.txt", Path.GetFileName(path)),
            AfterObservation = path => Assert.AreEqual("keep.txt", Path.GetFileName(path))
        });
        foreach (var workers in new[] { "1", "4" })
        foreach (var kind in Enum.GetValues<SourceStringMatchKind>().Select(kind => (SourceStringMatchKind?)kind).Append(null))
        foreach (var negated in new[] { false, true })
        foreach (var name in new[] { "matches", "many", "lines", "files", "bytes" })
        {
            var needle = (kind, negated) switch
            {
                (SourceStringMatchKind.Exact, false) => "keep.txt", (SourceStringMatchKind.Exact, true) => "reject.txt",
                (SourceStringMatchKind.Suffix, false) => "eep.txt", (SourceStringMatchKind.Suffix, true) => "ject.txt",
                (_, false) => "keep", _ => "reject"
            };
            SourcePredicatePatternMatch pattern = kind is null
                ? new SourcePredicateRegexMatch(new("Path"), needle, TimeSpan.FromMilliseconds(31), isNegated: negated)
                : new SourcePredicateStringMatch(new("Path"), kind.Value, kind switch
                { SourceStringMatchKind.Prefix => needle + "%", SourceStringMatchKind.Suffix => "%" + needle, SourceStringMatchKind.Contains => "%" + needle + "%", _ => needle }, needle, isNegated: negated);
            var context = Context(name, pattern, workers);
            Assert.AreSame(pattern, context.Plan.PredicateApplications.Single().Predicate);
            int Count<T>(RowSource<T> source) => source.Chunks.Sum(chunk => chunk.Count);
            var count = name switch
            {
                "matches" => Count(new SearchMatchesTypedSource(_root, "TODO", context)),
                "many" => Count(new SearchManyTypedSource(_root, [new SearchPatternInput("p", "TODO")], context)),
                "lines" => Count(new SearchLinesTypedSource(_root, "TODO", context)),
                "files" => Count(new SearchFilesTypedSource(_root, "TODO", context)),
                "bytes" => Count(new SearchBytesTypedSource(_root, "544f444f", context)),
                _ => throw new InvalidOperationException()
            };
            Assert.AreEqual(1, count, $"{name}/{kind}/{negated}/{workers}");
        }
    }

    [TestMethod]
    public void PathRejections_AvoidByteChargesButStillConsumeFileCountBudget()
    {
        var pattern = new SourcePredicateRegexMatch(new("Path"), "^keep", TimeSpan.FromMilliseconds(31));
        var source = new SearchMatchesSource(SearchRequest.Create(_root, "TODO", limits: new SearchResourceLimits(maxTotalBytes: 5)), Context("matches", pattern));
        Assert.AreEqual(1, source.Chunks.Sum(chunk => chunk.Count));
        Assert.AreEqual(2L, source.LastExecution!.VisitedCandidates);
        Assert.AreEqual(1L, source.LastExecution.EligibleFiles);
        Assert.AreEqual(1L, source.LastExecution.PathRejectedFiles);
        Assert.AreEqual(1L, source.LastExecution.FilesOpened);
        var limited = new SearchMatchesSource(SearchRequest.Create(_root, "TODO", limits: new SearchResourceLimits(maxFiles: 1)), Context("matches", pattern));
        Assert.AreEqual("file-count", Assert.ThrowsException<SearchResourceLimitException>(() => limited.Chunks.ToArray()).BudgetCode);
    }

    [TestMethod]
    public void EarlyPathAndLateText_KeepPredicateOnlyFieldsAndOriginalApplications()
    {
        var path = new SourcePredicateRegexMatch(new("Path"), "^keep", TimeSpan.FromMilliseconds(31));
        var text = new SourcePredicateStringMatch(new("MatchText"), SourceStringMatchKind.Exact, "TODO", "TODO");
        var plan = new SearchSchema().TryPlanSource("matches", new() { Identity = new("search", "s", "s", "matches"),
            Predicate = new SourcePredicateLogical(SourcePredicateLogicalOperator.And, path, text), Take = 1, RequiredColumns = [new("ByteOffset")] });
        Assert.AreEqual(2, plan.ExecutionPlan.PredicateApplications.Count);
        Assert.AreSame(path, plan.ExecutionPlan.PredicateApplications[0].Predicate);
        Assert.AreSame(text, plan.ExecutionPlan.PredicateApplications[1].Predicate);
        var source = new SearchMatchesSource(SearchRequest.Create(_root, "TODO"), RuntimeV2TestContexts.CreateExecutionContext(executionPlan: plan.ExecutionPlan));
        for (var i = 0; i < 2; i++)
        {
            var row = source.Chunks.SelectMany(chunk => chunk).Single();
            Assert.AreEqual("keep.txt", row.Path);
            Assert.AreEqual("TODO", row.MatchText);
        }
    }

    [TestMethod]
    public void CountsPathPredicate_StillScansExcludedContentWithTakeZero()
    {
        var plan = new SearchSchema().TryPlanSource("counts", new() { Identity = new("search", "s", "s", "counts"),
            Predicate = new SourcePredicateRegexMatch(new("Path"), "^keep", TimeSpan.FromMilliseconds(31)), Take = 0 });
        var source = new SearchCountsSource(SearchRequest.Create(_root, "TODO"), RuntimeV2TestContexts.CreateExecutionContext(executionPlan: plan.ExecutionPlan));
        Assert.AreEqual(0, source.Chunks.Sum(chunk => chunk.Count));
        Assert.AreEqual(2L, source.LastExecution!.FilesCompleted);
        Assert.IsTrue(source.LastExecution.CountsExact);
        Assert.AreEqual(0L, source.LastExecution.PathRejectedFiles);
    }

    [TestMethod]
    public void EffectiveScopeFingerprint_IncludesEarlyPredicateAndExecutionCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            var fingerprints = new List<string>();
            foreach (var culture in new[] { "en-US", "tr-TR" })
            foreach (var pattern in new[] { "keep", "reject" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                var source = new SearchMatchesSource(SearchRequest.Create(_root, "TODO"), Context("matches", new SourcePredicateRegexMatch(new("Path"), pattern, TimeSpan.FromMilliseconds(31))));
                _ = source.Chunks.ToArray();
                fingerprints.Add(source.LastExecution!.ScopeFingerprint);
            }
            Assert.AreEqual(4, fingerprints.Distinct().Count());
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    private static SourceExecutionContext Context(string source, SourcePredicateExpression predicate, string workers = "1")
    {
        var plan = new SearchSchema().TryPlanSource(source, new() { Identity = new("search", "s", "s", source), Predicate = predicate });
        return RuntimeV2TestContexts.CreateExecutionContext(executionPlan: plan.ExecutionPlan,
            sourceRuntimeSettings: new Dictionary<string, string> { ["search.max_parallelism"] = workers });
    }
}
