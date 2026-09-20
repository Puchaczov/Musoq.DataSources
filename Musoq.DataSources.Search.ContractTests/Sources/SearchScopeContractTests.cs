#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Search.ContractTests.Infrastructure;
using Musoq.DataSources.Search.Testing;

namespace Musoq.DataSources.Search.ContractTests.Sources;

[TestClass]
public sealed class SearchScopeContractTests : SearchContractTestBase
{
    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void RepositoryIgnores_ShouldKeepNegatedFileAndExcludeIgnoredFiles()
    {
        WithCorpus(ContractCorpusProfile.Scope, fixture =>
        {
            var scopeRoot = fixture.PathFor("scope");
            var result = Sql.ExecuteForRoot(
                scopeRoot,
                root => $"select m.Path from search.matches('{root}', 'TODO') m order by m.Path");

            CollectionAssert.AreEqual(
                SearchScopeOracle.EligibleTextPaths(fixture).ToArray(),
                Paths(result));
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void HiddenEntries_ShouldBeExcludedByDefaultAndIncludedExplicitly()
    {
        WithCorpus(ContractCorpusProfile.Scope, fixture =>
        {
            var scopeRoot = fixture.PathFor("scope");
            var hidden = Sql.ExecuteForRoot(
                scopeRoot,
                root => $"select m.Path from search.matches('{root}', 'TODO', " +
                        "(Scope: (HiddenEntries: true))) m order by m.Path");

            CollectionAssert.AreEqual(
                SearchScopeOracle.EligibleTextPaths(fixture, includeHidden: true).ToArray(),
                Paths(hidden));
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void DisabledRepositoryIgnores_ShouldExposeIgnoredContent()
    {
        WithCorpus(ContractCorpusProfile.Scope, fixture =>
        {
            var scopeRoot = fixture.PathFor("scope");
            var result = Sql.ExecuteForRoot(
                scopeRoot,
                root => $"select m.Path from search.matches('{root}', 'TODO', " +
                        "(Scope: (RepositoryIgnores: 'disabled'))) m order by m.Path");

            CollectionAssert.AreEqual(
                SearchScopeOracle.EligibleTextPaths(
                    fixture,
                    includeHidden: false,
                    respectRepositoryIgnores: false).ToArray(),
                Paths(result));
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void NonRecursiveScope_ShouldOnlyVisitDirectFiles()
    {
        WithCorpus(ContractCorpusProfile.Scope, fixture =>
        {
            var scopeRoot = fixture.PathFor("scope");
            var result = Sql.ExecuteForRoot(
                scopeRoot,
                root => $"select m.Path from search.matches('{root}', 'TODO', " +
                        "(Scope: (Recursive: false))) m order by m.Path");

            CollectionAssert.AreEqual(
                SearchScopeOracle.EligibleTextPaths(fixture, recursive: false).ToArray(),
                Paths(result));
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void ExplicitFileRoot_ShouldBypassDirectoryIgnoreRules()
    {
        WithCorpus(ContractCorpusProfile.Scope, fixture =>
        {
            var ignoredFile = fixture.PathFor("scope/ignored/secret.txt");
            var result = Sql.ExecuteForRoot(
                ignoredFile,
                root => $"select m.Path, m.MatchText from search.matches('{root}', 'TODO') m");

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("secret.txt", result.Value<string>(0, 0));
            Assert.AreEqual("TODO", result.Value<string>(0, 1));
        });
    }

    [TestMethod]
    [TestCategory("SearchContractFast")]
    public void IncludeAndExclude_ShouldRemainRootRelative()
    {
        WithCorpus(ContractCorpusProfile.Scope, fixture =>
        {
            var scopeRoot = fixture.PathFor("scope");
            var result = Sql.ExecuteForRoot(
                scopeRoot,
                root => $"select p.Path from search.paths('{root}', " +
                        "(Scope: (Include: array { '*.txt' }, Exclude: array { 'ignored/**' }))) p " +
                        "order by p.Path");

            CollectionAssert.AreEqual(new[] { "kept.txt" }, Paths(result));
        });
    }
}
