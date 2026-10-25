#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Search.Entities;
using Musoq.DataSources.Search.Sources;
using Musoq.DataSources.Search.Components.Contracts;
using Musoq.DataSources.Search.Components.Planning;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Schema;
using Musoq.Schema.Optimization;
namespace Musoq.DataSources.Search.Tests;

[TestClass]
public sealed class NativePatternTests
{
    private string _root = null!;
    [TestInitialize]
    public void Initialize()
    {
        _root = Path.Combine(Path.GetTempPath(), "musoq-pattern-search-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "Alpha.txt"), "TODO Alpha\nTODO beta\nTODO\n");
        File.WriteAllText(Path.Combine(_root, "Beta.txt"), "TODO gamma\n");
    }
    [TestCleanup]
    public void Cleanup() => Directory.Delete(_root, true);

    [TestMethod]
    public void AllSearchStringColumns_ExecuteWithPredicateOnlyProjection()
    {
        var schema = new SearchSchema();
        PatternContractVerifier.VerifySource(schema, "matches", [_root, "TODO"], context => new SearchMatchesTypedSource(_root, "TODO", context));
        PatternContractVerifier.VerifySource(schema, "many", [_root, new[] { new SearchPatternInput("alpha", "TODO") }], context => new SearchManyTypedSource(_root, new[] { new SearchPatternInput("alpha", "TODO") }, context));
        PatternContractVerifier.VerifySource(schema, "lines", [_root, "TODO"], context => new SearchLinesTypedSource(_root, "TODO", context));
        PatternContractVerifier.VerifySource(schema, "files", [_root, "TODO"], context => new SearchFilesTypedSource(_root, "TODO", context));
        PatternContractVerifier.VerifySource(schema, "counts", [_root, "TODO"], context => new SearchCountsTypedSource(_root, "TODO", context));
        PatternContractVerifier.VerifySource(schema, "paths", [_root], context => new SearchPathsTypedSource(_root, context));
        PatternContractVerifier.VerifySource(schema, "bytes", [_root, "544F444F"], context => new SearchBytesTypedSource(_root, "544F444F", context));
    }

    [TestMethod]
    public void AuditStringPatterns_FilterCompletedRowsBeforePagingAndAlwaysCompleteScan()
    {
        var schema = new SearchSchema();
        var identity = new SourceIdentity("search", "s", "s", "audit");
        var empty = RuntimeV2TestContexts.CreateExecutionContext(executionPlan: SourceExecutionPlan.Empty(identity));
        var descriptor = schema.DescribeSource("audit", new(identity, empty), _root, "TODO");
        var strings = descriptor.Columns.Where(column => column.ColumnType == typeof(string)).ToArray();
        CollectionAssert.AreEquivalent(strings.Select(column => column.ColumnName).ToArray(), descriptor.PredicateCapabilities.StringMatches.Select(item => item.Column.Name).ToArray());
        var baseline = new SearchAuditSource(SearchRequest.Create(_root, "TODO"), empty).Chunks.SelectMany(chunk => chunk).Single();
        foreach (var column in strings)
        foreach (var kind in Enum.GetValues<SourceStringMatchKind>().Select(value => (SourceStringMatchKind?)value).Append(null))
        foreach (var negated in new[] { false, true })
        foreach (var take in new[] { 0L, 1L })
        {
            var regex = kind is null;
            var pattern = kind switch { SourceStringMatchKind.Prefix or SourceStringMatchKind.Suffix => "%", SourceStringMatchKind.Contains => "%%", _ => "" };
            SourcePredicatePatternMatch predicate = regex
                ? new SourcePredicateRegexMatch(new(column.ColumnName), "^$", TimeSpan.FromMilliseconds(17), isNegated: negated)
                : new SourcePredicateStringMatch(new(column.ColumnName), kind!.Value, pattern, "", isNegated: negated);
            var plan = schema.TryPlanSource("audit", new() { Identity = identity, Predicate = predicate, Take = take, RequiredColumns = [new("Occurrences")] });
            Assert.AreSame(predicate, plan.AcceptedPredicate);
            Assert.AreSame(predicate, plan.ExecutionPlan.PredicateApplications.Single().Predicate);
            Assert.AreEqual(SourcePredicateEvaluationPhase.RowFiltering, plan.ExecutionPlan.PredicateApplications.Single().Phase);
            var source = new SearchAuditSource(SearchRequest.Create(_root, "TODO"), RuntimeV2TestContexts.CreateExecutionContext(executionPlan: plan.ExecutionPlan));
            var rows = source.Chunks.SelectMany(chunk => chunk).ToArray();
            var value = (string?)typeof(SearchAudit).GetProperty(column.ColumnName)!.GetValue(baseline);
            var expected = (regex ? Operators.RLikeDynamic(value, "^$") : Operators.LikePrepared(value, Operators.PrepareLike(pattern))) != negated;
            Assert.AreEqual(take == 1 && expected ? 1 : 0, rows.Length, column.ColumnName);
            Assert.AreEqual(2L, source.LastExecution!.FilesCompleted);
            Assert.AreEqual(4L, source.LastExecution.Occurrences);
            Assert.IsTrue(source.LastExecution.CountsExact);
        }
    }

    [TestMethod]
    public void Patterns_KeepNestedAndOrExpressionsResidual()
    {
        var schema = new SearchSchema();
        var pattern = new SourcePredicateStringMatch(new("MatchText"), SourceStringMatchKind.Prefix, "T%", "T");
        foreach (SourcePredicateExpression predicate in new SourcePredicateExpression[]
        {
            new SourcePredicateStringMatch(new("Captures.Value"), SourceStringMatchKind.Prefix, "T%", "T"),
            new SourcePredicateLogical(SourcePredicateLogicalOperator.Or, pattern, pattern)
        })
        {
            var plan = schema.TryPlanSource("matches", new() { Identity = new("search", "s", "s", "matches"), Predicate = predicate, Take = 1 });
            Assert.AreSame(predicate, plan.ResidualPredicate);
            Assert.IsNull(plan.AcceptedTake);
            Assert.AreEqual(0, plan.ExecutionPlan.PredicateApplications.Count);
        }
    }

    [TestMethod]
    public void RetainedTextPatterns_RepeatedAndConcurrentExecutionsRemainIsolated()
    {
        var predicate = new SourcePredicateStringMatch(new("LineText"), SourceStringMatchKind.Contains, "%Alpha%", "Alpha");
        var plan = new SearchSchema().TryPlanSource("lines", new() { Identity = new("search", "s", "s", "lines"), Predicate = predicate, RequiredColumns = [new("Path")] });
        Parallel.For(0, 8, _ =>
        {
            var source = new SearchLinesTypedSource(_root, "TODO", RuntimeV2TestContexts.CreateExecutionContext(executionPlan: plan.ExecutionPlan));
            foreach (var repetition in new[] { 1, 2 })
            {
                var rows = source.Chunks.SelectMany(chunk => chunk).ToArray();
                Assert.AreEqual(1, rows.Length);
                Assert.AreEqual("TODO Alpha\n", rows[0].LineText);
            }
        });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var canceled = new SearchLinesTypedSource(_root, "TODO", RuntimeV2TestContexts.CreateExecutionContext(cancellationToken: cancellation.Token, executionPlan: plan.ExecutionPlan));
        Assert.ThrowsException<OperationCanceledException>(() => canceled.Chunks.SelectMany(chunk => chunk).ToArray());
    }
}
