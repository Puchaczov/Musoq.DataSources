#nullable enable

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Search.Benchmarks.Comparisons;
using Musoq.DataSources.Search.Benchmarks.Measurements;

namespace Musoq.DataSources.Search.Tests.Benchmarks.Measurements;

[TestClass]
public sealed class SearchDecodingBenchmarkTests
{
    [TestMethod]
    public void DecodingCohorts_ShouldPreserveCompleteMatchResults()
    {
        foreach (var cohort in SearchDecodingCorpus.Cohorts)
        {
            using var corpus = SearchDecodingCorpus.Create(cohort);
            var expected = corpus.ExpectedMatches
                .Select(SearchSpikeResultFormatter.Signature)
                .ToArray();

            var result = ManagedSearchRunner.Run(
                corpus.Root,
                corpus.PatternSet);

            CollectionAssert.AreEqual(
                expected,
                result.Matches
                    .Select(SearchSpikeResultFormatter.Signature)
                    .ToArray(),
                $"The {cohort.Id} {cohort.EncodingName} decoding cohort changed match coordinates.");
            Assert.AreEqual(corpus.EligibleBytes, result.BytesRead);
            Assert.IsTrue(result.FilesVisited > 0);
            Assert.IsNotNull(result.StageTimings);
        }
    }
}
