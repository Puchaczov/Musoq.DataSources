#nullable enable

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Search.Benchmarks.Comparisons;
using Musoq.DataSources.Search.Benchmarks.Measurements;

namespace Musoq.DataSources.Search.Tests.Benchmarks.Measurements;

[TestClass]
public sealed class BatchedSearchWorkflowBenchmarkTests
{
    [TestMethod]
    public void BatchedWorkloads_ShouldPreserveLabeledOccurrencesAgainstTheIndependentOracle()
    {
        foreach (var cohort in BatchedSearchWorkflow.Cohorts)
        {
            using var corpus = BatchedSearchCorpus.Create(cohort);
            foreach (var patternCount in BatchedSearchWorkflow.PatternCounts)
            {
                var patterns = BatchedSearchWorkflow.CreatePatterns(patternCount);
                var expected = corpus.GetExpectedMatches(patterns)
                    .Select(SearchSpikeResultFormatter.Signature)
                    .ToArray();
                var managed = BatchedSearchWorkflow.RunManaged(corpus, patterns);

                CollectionAssert.AreEqual(
                    expected,
                    managed.Result.Matches
                        .Select(SearchSpikeResultFormatter.Signature)
                        .ToArray(),
                    $"Managed output changed for {cohort.Id}/{patternCount}.");
                Assert.AreEqual(1, managed.Result.ProcessInvocations);
                Assert.AreEqual(corpus.FileCount, managed.Result.FilesVisited);
                Assert.AreEqual(corpus.FileCount, managed.CandidatesYielded);
                Assert.IsTrue(managed.ScannedBytes > 0);
                Assert.IsTrue(expected.Length > 0);
            }
        }
    }

    [TestMethod]
    public void SparseAndDenseCohorts_ShouldExerciseDifferentHitDistributions()
    {
        var sparse = BatchedSearchWorkflow.Cohorts.Single(
            cohort => cohort.Id == "sparse");
        var dense = BatchedSearchWorkflow.Cohorts.Single(
            cohort => cohort.Id == "dense");
        var patterns = BatchedSearchWorkflow.CreatePatterns(1);

        using var sparseCorpus = BatchedSearchCorpus.Create(sparse);
        using var denseCorpus = BatchedSearchCorpus.Create(dense);
        var sparseResult = BatchedSearchWorkflow.RunManaged(
            sparseCorpus,
            patterns);
        var denseResult = BatchedSearchWorkflow.RunManaged(
            denseCorpus,
            patterns);

        Assert.IsTrue(denseResult.Result.Matches.Count > sparseResult.Result.Matches.Count);
        Assert.AreEqual(sparseCorpus.EligibleBytes, sparseResult.ScannedBytes);
        Assert.AreEqual(denseCorpus.EligibleBytes, denseResult.ScannedBytes);
    }

    [TestMethod]
    public void AutomatonStructure_ShouldGrowAcrossTheRegisteredPatternCounts()
    {
        var snapshots = BatchedSearchWorkflow.PatternCounts
            .Select(patternCount => BatchedSearchWorkflow.MeasureAutomaton(
                BatchedSearchWorkflow.CreatePatterns(patternCount)))
            .ToArray();

        Assert.AreEqual(3, snapshots.Length);
        CollectionAssert.AreEqual(
            BatchedSearchWorkflow.PatternCounts.ToArray(),
            snapshots.Select(snapshot => snapshot.PatternCount).ToArray());
        Assert.IsTrue(snapshots[0].NodeCount < snapshots[1].NodeCount);
        Assert.IsTrue(snapshots[1].NodeCount < snapshots[2].NodeCount);
        Assert.IsTrue(snapshots[0].TransitionCount < snapshots[1].TransitionCount);
        Assert.IsTrue(snapshots[1].TransitionCount < snapshots[2].TransitionCount);
        Assert.IsTrue(snapshots[0].OutputReferenceCount < snapshots[1].OutputReferenceCount);
        Assert.IsTrue(snapshots[1].OutputReferenceCount < snapshots[2].OutputReferenceCount);
        Assert.IsTrue(snapshots.All(snapshot => snapshot.ConstructionAllocatedBytes > 0));
    }
}
