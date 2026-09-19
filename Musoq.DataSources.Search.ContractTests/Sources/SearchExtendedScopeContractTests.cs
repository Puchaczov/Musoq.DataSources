#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Search.ContractTests.Infrastructure;
using Musoq.DataSources.Search.Testing;
using Musoq.Evaluator.Exceptions;

namespace Musoq.DataSources.Search.ContractTests.Sources;

[TestClass]
public sealed class SearchExtendedScopeContractTests : SearchContractTestBase
{
    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void ScopeEligibility_ShouldBeSharedByPathsCountsFilesAndAudit()
    {
        WithExtendedCorpus(fixture =>
        {
            var scopeRoot = fixture.PathFor("extended/scope");
            var paths = Sql.ExecuteForRoot(
                scopeRoot,
                root => $"select p.Path from search.paths('{root}') p order by p.Path");
            var counts = Sql.ExecuteForRoot(
                scopeRoot,
                root => $"select c.Path from search.counts('{root}', 'NEVER') c order by c.Path");
            var files = Sql.ExecuteForRoot(
                scopeRoot,
                root => $"select f.Path from search.files('{root}', 'TODO') f order by f.Path");
            var audit = Sql.ExecuteForRoot(
                scopeRoot,
                root => $"select a.EligibleFiles, a.FilesCompleted, a.CountsExact from search.audit('{root}', 'NEVER') a");

            CollectionAssert.AreEqual(Paths(paths), Paths(counts));
            Assert.IsTrue(paths.Count > 0);
            Assert.IsTrue(files.Count > 0);
            Assert.AreEqual(paths.Count, audit.Value<long>(0, 0));
            Assert.AreEqual(paths.Count, audit.Value<long>(0, 1));
            Assert.IsTrue(audit.Value<bool>(0, 2));
            CollectionAssert.Contains(Paths(paths), "keep.tmp");
            CollectionAssert.DoesNotContain(Paths(paths), "drop.tmp");
            CollectionAssert.DoesNotContain(Paths(paths), ".hidden.txt");
            CollectionAssert.DoesNotContain(Paths(paths), "nested/secret.secret");
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void ScopeIncludeExcludeAndMetadata_ShouldUseRootRelativeTypedOptions()
    {
        WithExtendedCorpus(fixture =>
        {
            var scopeRoot = fixture.PathFor("extended/scope");
            var selected = Sql.ExecuteForRoot(
                scopeRoot,
                root => $"select p.Path from search.paths('{root}', " +
                        "(Scope: (Include: array { '*.txt' }, Exclude: array { 'metadata-*' }, " +
                        "Metadata: (ExtensionIncludes: array { '.txt' }, MinimumSizeBytes: 5)))) p " +
                        "order by p.Path");

            CollectionAssert.AreEqual(new[] { "keep.txt" }, Paths(selected));
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void MetadataModificationBounds_ShouldBeInclusiveAndDeterministic()
    {
        WithExtendedCorpus(fixture =>
        {
            var root = fixture.PathFor("extended/scope");
            var selected = Sql.ExecuteForRoot(
                root,
                escaped => $"select p.Path from search.paths('{escaped}', " +
                           "(Scope: (Metadata: (ModifiedAfterOrEqualUtc: '2024-04-29T15:06:40.0000000Z', " +
                           "ModifiedBeforeOrEqualUtc: '2024-04-29T15:06:40.0000000Z')))) p " +
                           "order by p.Path");

            CollectionAssert.AreEqual(
                new[] { "metadata-large.txt" },
                Paths(selected));
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void ScopeFingerprint_ShouldChangeWhenRequestedPolicyChanges()
    {
        WithExtendedCorpus(fixture =>
        {
            var root = fixture.PathFor("extended/scope");
            var defaultFingerprint = Sql.ExecuteForRoot(
                root,
                escaped => $"select a.ScopeFingerprint from search.audit('{escaped}', 'TODO') a")
                .Value<string>(0, 0);
            var nonRecursiveFingerprint = Sql.ExecuteForRoot(
                root,
                escaped => $"select a.ScopeFingerprint from search.audit('{escaped}', 'TODO', " +
                           "(Scope: (Recursive: false))) a")
                .Value<string>(0, 0);
            var hiddenFingerprint = Sql.ExecuteForRoot(
                root,
                escaped => $"select a.ScopeFingerprint from search.audit('{escaped}', 'TODO', " +
                           "(Scope: (HiddenEntries: true))) a")
                .Value<string>(0, 0);

            Assert.AreNotEqual(defaultFingerprint, nonRecursiveFingerprint);
            Assert.AreNotEqual(defaultFingerprint, hiddenFingerprint);
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void GlobalIgnoreOptions_ShouldRejectAmbiguousTypedCombinations()
    {
        WithExtendedCorpus(fixture =>
        {
            var root = fixture.PathFor("extended/scope");
            Assert.ThrowsException<QueryExecutionException>(() =>
                Sql.ExecuteForRoot(
                    root,
                    escaped => $"select p.Path from search.paths('{escaped}', " +
                                "(Scope: (GlobalIgnores: 'disabled', GlobalIgnoreRules: array { '*.tmp' }))) p"));
            Assert.ThrowsException<QueryExecutionException>(() =>
                Sql.ExecuteForRoot(
                    root,
                    escaped => $"select p.Path from search.paths('{escaped}', " +
                                "(Scope: (GlobalIgnores: 'configured'))) p"));
        });
    }
}
