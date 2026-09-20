#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Search.Components.Contracts;
using Musoq.DataSources.Search.Components.Diagnostics;
using Musoq.DataSources.Search.Components.Traversal;
using Musoq.DataSources.Search.Components.Testing;
using Musoq.DataSources.Search.ContractTests.Infrastructure;
using Musoq.DataSources.Search.Sources;
using Musoq.DataSources.Search.Testing;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator.Exceptions;

namespace Musoq.DataSources.Search.ContractTests.Sources;

[TestClass]
public sealed class SearchCompletionContractTests : SearchContractTestBase
{
    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void Audit_ShouldReturnOneFreshCompleteSummary()
    {
        WithCorpus(ContractCorpusProfile.Minimal, fixture =>
        {
            var first = ReadAudit(fixture.Root);
            var second = ReadAudit(fixture.Root);

            Assert.AreEqual(1, first.Count);
            Assert.AreEqual(1, second.Count);
            Assert.AreNotEqual(first.Value<string>(0, 0), second.Value<string>(0, 0));
            Assert.AreEqual(first.Value<string>(0, 1), second.Value<string>(0, 1));
            Assert.AreEqual(1, first.Value<int>(0, 2));
            Assert.AreEqual("completed-scan", first.Value<string>(0, 3));
            Assert.IsTrue(first.Value<bool>(0, 4));
            Assert.IsTrue(first.Value<bool>(0, 5));
            Assert.IsTrue(first.Value<bool>(0, 6));
            Assert.AreEqual(8L, first.Value<long>(0, 7));
            Assert.AreEqual(7L, first.Value<long>(0, 8));
            Assert.IsNull(first.Value(0, 9));
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void Audit_ShouldObserveCurrentContentOnEveryInvocation()
    {
        WithCorpus(ContractCorpusProfile.Minimal, fixture =>
        {
            var first = ReadAudit(fixture.Root);
            File.WriteAllText(fixture.PathFor("top.txt"), "DONE\n");
            var second = ReadAudit(fixture.Root);

            Assert.AreEqual(7L, first.Value<long>(0, 8));
            Assert.AreEqual(5L, second.Value<long>(0, 8));
            Assert.AreEqual("completed-scan", second.Value<string>(0, 3));
            Assert.IsTrue(second.Value<bool>(0, 4));
            Assert.IsTrue(second.Value<bool>(0, 5));
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void MissingRootAudit_ShouldBeAResolvedFailureNotAnEmptySuccess()
    {
        var missingRoot = Path.Combine(Path.GetTempPath(), $"musoq-search-contract-missing-{Guid.NewGuid():N}");
        var result = Sql.ExecuteForRoot(
            missingRoot,
            root => $"select a.Outcome, a.TerminalReason, a.Complete, a.ScopeResolved, " +
                    $"a.CountsExact, a.FailureCode from search.audit('{root}', 'TODO') a");

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(2, result.Value<int>(0, 0));
        Assert.AreEqual("root-missing", result.Value<string>(0, 1));
        Assert.IsFalse(result.Value<bool>(0, 2));
        Assert.IsFalse(result.Value<bool>(0, 3));
        Assert.IsFalse(result.Value<bool>(0, 4));
        Assert.IsNotNull(result.Value(0, 5));
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void Take_ShouldLimitAcceptedRowsWithoutClaimingWholeScopeAbsence()
    {
        WithCorpus(ContractCorpusProfile.Minimal, fixture =>
        {
            var result = Sql.ExecuteForRoot(
                fixture.Root,
                root => $"select m.Path, m.MatchIndex from search.matches('{root}', 'TODO') m take 1");

            Assert.AreEqual(1, result.Count);
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void Paths_ShouldEnumerateWithoutOpeningFileContent()
    {
        WithCorpus(ContractCorpusProfile.Minimal, fixture =>
        {
            var counters = new SearchScopeCounters();
            var source = new SearchPathsSource(
                fixture.Root,
                RuntimeV2TestContexts.CreateExecutionContext(),
                ScopePolicy.Default,
                counters);

            var rows = source.Chunks.SelectMany(chunk => chunk).ToArray();

            Assert.AreEqual(fixture.Files.Count, rows.Length);
            Assert.AreEqual(0L, counters.ContentOpenAttempts);
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void InvalidOptions_ShouldFailBeforeOpeningTheRoot()
    {
        var missingRoot = Path.Combine(Path.GetTempPath(), $"musoq-search-contract-invalid-{Guid.NewGuid():N}");

        var exception = Assert.ThrowsException<SearchRequestException>(() =>
            new SearchMatchesTypedSource(
                missingRoot,
                "TODO",
                new SearchMatchOptionsInput(records: new SearchRecordsInput()),
                RuntimeV2TestContexts.CreateExecutionContext()));

        Assert.AreEqual("records", exception.Diagnostic.Location?.ArgumentName);
    }

    [TestMethod]
    [TestCategory("SearchContractFault")]
    public void ControlledReadFailure_ShouldProduceOneFailedAuditOutcome()
    {
        WithCorpus(ContractCorpusProfile.Minimal, fixture =>
        {
            var failingPath = fixture.PathFor("nested/child.txt");
            using var hooks = SearchTestHooks.Install(new SearchTestHooksState
            {
                BeforeContentOpen = path =>
                {
                    if (string.Equals(path, failingPath, StringComparison.OrdinalIgnoreCase))
                        throw new SearchSourceReadException(
                            SearchDiagnosticCatalog.SourceReadFailed(path));
                }
            });

            var result = Sql.ExecuteForRoot(
                fixture.Root,
                root => $"select a.Outcome, a.Complete, a.CountsExact, a.FailurePath " +
                        $"from search.audit('{root}', 'TODO') a");

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(2, result.Value<int>(0, 0));
            Assert.IsFalse(result.Value<bool>(0, 1));
            Assert.IsFalse(result.Value<bool>(0, 2));
            StringAssert.EndsWith(result.Value<string>(0, 3), "nested\\child.txt");
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFault")]
    public void ControlledMutation_ShouldProduceAStaleSourceFailure()
    {
        WithCorpus(ContractCorpusProfile.Minimal, fixture =>
        {
            var mutatedPath = fixture.PathFor("nested/child.txt");
            var mutationCount = 0;
            using var hooks = SearchTestHooks.Install(new SearchTestHooksState
            {
                AfterObservation = path =>
                {
                    if (string.Equals(path, mutatedPath, StringComparison.OrdinalIgnoreCase) &&
                        Interlocked.Exchange(ref mutationCount, 1) == 0)
                    {
                        File.WriteAllText(mutatedPath, "TODO changed\n");
                    }
                }
            });

            var result = Sql.ExecuteForRoot(
                fixture.Root,
                root => $"select a.Outcome, a.Complete, a.CountsExact, a.FailurePath " +
                        $"from search.audit('{root}', 'TODO') a");

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(2, result.Value<int>(0, 0));
            Assert.IsFalse(result.Value<bool>(0, 1));
            Assert.IsFalse(result.Value<bool>(0, 2));
            StringAssert.EndsWith(result.Value<string>(0, 3), "nested\\child.txt");
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFault")]
    public void CancellationCheckpoint_ShouldCancelARealTypedSource()
    {
        using var fixture = ContractCorpusBuilder.Create(ManifestPath, ContractCorpusProfile.Text);
        using var cancellation = new CancellationTokenSource();
        using var hooks = SearchTestHooks.Install(new SearchTestHooksState
        {
            Checkpoint = cancellation.Cancel
        });

        var source = new SearchMatchesTypedSource(
            fixture.Root,
            "TODO",
            RuntimeV2TestContexts.CreateExecutionContext(cancellation.Token));

        Assert.ThrowsException<OperationCanceledException>(() =>
            source.Chunks.SelectMany(chunk => chunk).ToArray());
    }

    private static SearchQueryResult ReadAudit(string root)
    {
        return Sql.ExecuteForRoot(
            root,
            escaped => $"select a.ScanId, a.ScopeFingerprint, a.Outcome, a.TerminalReason, " +
                       "a.Complete, a.ScopeExhausted, a.CountsExact, a.EligibleFiles, " +
                       "a.Occurrences, a.FailureCode from search.audit('" + escaped + "', 'TODO') a");
    }
}
