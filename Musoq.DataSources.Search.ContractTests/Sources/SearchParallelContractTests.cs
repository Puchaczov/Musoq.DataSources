#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Search.ContractTests.Infrastructure;
using Musoq.DataSources.Search.Testing;

namespace Musoq.DataSources.Search.ContractTests.Sources;

[TestClass]
public sealed class SearchParallelContractTests : SearchContractTestBase
{
    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void SequentialAndParallelExecution_ShouldHaveIdenticalNormalizedRows()
    {
        WithCorpus(ContractCorpusProfile.Text, fixture =>
        {
            var sequential = ExecuteMatches(fixture, "1", "65536");
            var parallel = ExecuteMatches(fixture, "4", "65536");

            Assert.AreEqual(NormalizedSignature(sequential), NormalizedSignature(parallel));
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public async Task ConcurrentQueries_ShouldNotShareMutableMatcherState()
    {
        using var fixture = ContractCorpusBuilder.Create(ManifestPath, ContractCorpusProfile.Minimal);
        var tasks = Enumerable.Range(0, 4)
            .Select(_ => Task.Run(() => ExecuteMatches(fixture, "4", "65536")))
            .ToArray();

        try
        {
            var results = await Task.WhenAll(tasks);
            var expected = NormalizedSignature(results[0]);
            Assert.IsTrue(results.Skip(1).All(result => NormalizedSignature(result) == expected));
        }
        catch (Exception exception)
        {
            fixture.KeepOnDispose = true;
            fixture.WriteFailureReport(exception);
            throw;
        }
    }

    [TestMethod]
    [TestCategory("SearchContractScale")]
    public void ScaleCorpus_ShouldRemainCorrectWithHigherParallelism()
    {
        WithCorpus(ContractCorpusProfile.Scale, fixture =>
        {
            var sequential = ExecuteMatches(fixture, "1", "65536");
            var parallel = ExecuteMatches(fixture, "8", "131072");

            Assert.AreEqual(NormalizedSignature(sequential), NormalizedSignature(parallel));
            Assert.AreEqual(20, parallel.Count);
            Assert.IsTrue(parallel.Rows.All(row => row.Count == 4));
        });
    }

    private static SearchQueryResult ExecuteMatches(
        ContractCorpusFixture fixture,
        string parallelism,
        string bufferedOutputBytes)
    {
        return Sql.ExecuteForRoot(
            fixture.Root,
            root => $"select m.Path, m.MatchIndex, m.LineNumber, m.Utf16Column " +
                    $"from search.matches('{root}', 'TODO') m",
            new Dictionary<string, string>
            {
                ["search.max_parallelism"] = parallelism,
                ["search.buffered_output_bytes"] = bufferedOutputBytes
            });
    }

    private static string NormalizedSignature(SearchQueryResult result)
    {
        return string.Join(
            "\n",
            result.Rows
                .Select(row => string.Join("|", row.Select(value => value?.ToString() ?? "<null>")))
                .OrderBy(value => value, StringComparer.Ordinal));
    }
}
