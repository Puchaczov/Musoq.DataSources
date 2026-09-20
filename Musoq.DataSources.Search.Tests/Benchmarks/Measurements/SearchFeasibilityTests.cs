#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Search.Benchmarks.Comparisons;

namespace Musoq.DataSources.Search.Tests.Benchmarks.Measurements;

[TestClass]
public sealed class SearchFeasibilityTests
{
    [TestMethod]
    public void ManagedCandidate_ShouldObserveCancellationWithoutReturningPartialSuccess()
    {
        using var corpus = SearchSpikeCorpus.Create();

        Assert.ThrowsException<OperationCanceledException>(() =>
            ManagedSearchRunner.Run(
                corpus.Root,
                SearchSpikeCorpus.Patterns,
                CancellationToken.None,
                cancelAfterFiles: 1));
    }

    [TestMethod]
    public void MeasurementCohorts_ShouldPreserveTheirDeclaredMatchDistributions()
    {
        var observedCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var cohort in SearchSpikeCorpus.MeasurementCohorts)
        {
            using var corpus = SearchSpikeCorpus.Create(cohort);
            var result = ManagedSearchRunner.Run(corpus.Root, corpus.PatternSet);
            var expected = corpus.ExpectedMatches
                .Select(SearchSpikeResultFormatter.Signature)
                .ToArray();

            CollectionAssert.AreEqual(
                expected,
                result.Matches.Select(SearchSpikeResultFormatter.Signature).ToArray());
            Assert.IsNotNull(result.StageTimings);
            observedCounts.Add(cohort.Id, result.Matches.Count);
        }

        Assert.AreEqual(0, observedCounts["B02"]);
        Assert.IsTrue(observedCounts["B03"] > observedCounts["B02"]);
        Assert.IsTrue(observedCounts["B04"] > observedCounts["B03"]);
    }
}
