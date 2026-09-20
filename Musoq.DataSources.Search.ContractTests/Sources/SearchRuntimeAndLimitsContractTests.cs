#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Search;
using Musoq.DataSources.Search.Components.Diagnostics;
using Musoq.DataSources.Search.Components.Testing;
using Musoq.DataSources.Search.Components.Traversal;
using Musoq.DataSources.Search.ContractTests.Infrastructure;
using Musoq.DataSources.Search.Sources;
using Musoq.DataSources.Search.Testing;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator.Exceptions;

namespace Musoq.DataSources.Search.ContractTests.Sources;

[TestClass]
public sealed class SearchRuntimeAndLimitsContractTests : SearchContractTestBase
{
    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void RuntimeSettings_ShouldAcceptDocumentedBoundsAndPreserveRows()
    {
        WithCorpus(ContractCorpusProfile.Text, fixture =>
        {
            var baseline = Execute(fixture, "0", "33554432");
            var sequential = Execute(fixture, "1", "65536");
            var maximum = Execute(fixture, "32", "536870912");

            Assert.AreEqual(NormalizedSignature(baseline), NormalizedSignature(sequential));
            Assert.AreEqual(NormalizedSignature(baseline), NormalizedSignature(maximum));
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFault")]
    public void InvalidRuntimeSettings_ShouldFailBeforeOpeningContent()
    {
        WithCorpus(ContractCorpusProfile.Minimal, fixture =>
        {
            foreach (var settings in new[]
                     {
                         new Dictionary<string, string>
                         {
                             ["search.max_parallelism"] = "33"
                         },
                         new Dictionary<string, string>
                         {
                             ["search.buffered_output_bytes"] = "65535"
                         },
                         new Dictionary<string, string>
                         {
                             ["search.buffered_output_bytes"] = "536870913"
                         },
                         new Dictionary<string, string>
                         {
                             ["search.max_parallelism"] = "not-a-number"
                         }
                     })
            {
                var opened = 0;
                using var hooks = SearchTestHooks.Install(new SearchTestHooksState
                {
                    BeforeContentOpen = _ => Interlocked.Increment(ref opened)
                });

                var context = RuntimeV2TestContexts.CreateExecutionContext(
                    sourceRuntimeSettings: settings);
                Assert.ThrowsException<ArgumentException>(() =>
                    SearchSqlHarness.MaterializeTyped(
                        new SearchMatchesTypedSource(fixture.Root, "TODO", context).Chunks));
                Assert.AreEqual(0, opened, string.Join(",", settings.Select(pair => $"{pair.Key}={pair.Value}")));
            }
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void RuntimeSettings_ShouldExposeStableDescriptionsAndDefaults()
    {
        var empty = SearchFileParallelOptions.FromRuntimeSettings(new Dictionary<string, string>());
        var zero = SearchFileParallelOptions.FromRuntimeSettings(
            new Dictionary<string, string>
            {
                ["search.max_parallelism"] = "0",
                ["search.buffered_output_bytes"] = "65536"
            });

        Assert.AreEqual(Math.Clamp(Environment.ProcessorCount * 2, 1, 8), empty.WorkerCount);
        Assert.AreEqual(empty.WorkerCount, empty.MaxInFlightFiles / 2);
        Assert.AreEqual(empty.WorkerCount, zero.WorkerCount);
        Assert.AreEqual(65536L, zero.BufferedOutputBytes);
        Assert.AreEqual(32L * 1024 * 1024, empty.BufferedOutputBytes);
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void Paths_MaxFiles_ShouldLimitOnlyTheTypedPathResult()
    {
        WithCorpus(ContractCorpusProfile.Minimal, fixture =>
        {
            var result = Sql.ExecuteForRoot(
                fixture.Root,
                root => $"select p.Path from search.paths('{root}', (MaxFiles: 2)) p order by p.Path");

            Assert.AreEqual(2, result.Count);
            Assert.IsTrue(result.Rows.All(row => row[0] is string));
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFault")]
    public void WorkLimits_ShouldFailStrictlyInsteadOfReturningAFalseExactResult()
    {
        WithCorpus(ContractCorpusProfile.Minimal, fixture =>
        {
            var matchException = Assert.ThrowsException<QueryExecutionException>(() =>
                Sql.ExecuteForRoot(
                    fixture.PathFor("top.txt"),
                    root => $"select m.MatchIndex from search.matches('{root}', 'TODO', " +
                            "(Limits: (MaxMatchCount: 1))) m"));
            Assert.IsNotNull(FindInner<SearchResourceLimitException>(matchException), matchException.ToString());

            var fileException = Assert.ThrowsException<QueryExecutionException>(() =>
                Sql.ExecuteForRoot(
                    fixture.PathFor("top.txt"),
                    root => $"select m.MatchIndex from search.matches('{root}', 'TODO', " +
                            "(Limits: (MaxFileBytes: 1))) m"));
            Assert.IsNotNull(FindInner<SearchResourceLimitException>(fileException), fileException.ToString());
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFault")]
    public void TypedInputLimits_ShouldRejectEmptyAndOversizedPatternSetsBeforeRead()
    {
        var root = Path.Combine(Path.GetTempPath(), $"musoq-search-limit-contract-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var context = RuntimeV2TestContexts.CreateExecutionContext();
            Assert.ThrowsException<SearchResourceLimitException>(() =>
                new SearchManyTypedSource(root, [], context));

            Assert.ThrowsException<SearchResourceLimitException>(() =>
                new SearchManyTypedSource(
                    root,
                    [new SearchPatternInput("tooLong", new string('x', 65_537))],
                    context)
                    .Chunks
                    .ToArray());
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static SearchQueryResult Execute(
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

    private static T? FindInner<T>(Exception exception)
        where T : Exception
    {
        for (var current = exception; current is not null; current = current.InnerException)
            if (current is T match)
                return match;

        return null;
    }
}
