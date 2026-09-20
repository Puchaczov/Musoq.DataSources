#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Search;
using Musoq.DataSources.Search.Components.Diagnostics;
using Musoq.DataSources.Search.Components.Testing;
using Musoq.DataSources.Search.ContractTests.Infrastructure;
using Musoq.DataSources.Search.Sources;
using Musoq.DataSources.Search.Testing;
using Musoq.DataSources.Tests.Common;

namespace Musoq.DataSources.Search.ContractTests.Sources;

[TestClass]
public sealed class SearchTypedValidationContractTests : SearchContractTestBase
{
    [TestMethod]
    [TestCategory("SearchContractFault")]
    public void InvalidTextAndRecordCombinations_ShouldNameTheOptionPath()
    {
        var root = Path.Combine(Path.GetTempPath(), $"musoq-search-validation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var context = RuntimeV2TestContexts.CreateExecutionContext();
            var cases = new (Action Action, string Location)[]
            {
                (
                    () => new SearchMatchesTypedSource(
                        root,
                        "TODO",
                        new SearchMatchOptionsInput(
                            text: new SearchTextInput(mode: "glob")),
                        context),
                    "text.mode"),
                (
                    () => new SearchMatchesTypedSource(
                        root,
                        "TODO",
                        new SearchMatchOptionsInput(
                            text: new SearchTextInput(caseMode: "smart")),
                        context),
                    "case"),
                (
                    () => new SearchMatchesTypedSource(
                        root,
                        "TODO",
                        new SearchMatchOptionsInput(
                            text: new SearchTextInput(encoding: "ascii")),
                        context),
                    "encoding"),
                (
                    () => new SearchMatchesTypedSource(
                        root,
                        "TODO",
                        new SearchMatchOptionsInput(
                            records: new SearchRecordsInput()),
                        context),
                    "records")
            };

            foreach (var (action, location) in cases)
                AssertDiagnostic(action, location);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    [TestCategory("SearchContractFault")]
    public void InvalidScopeAndPathOptions_ShouldFailBeforeContentAccess()
    {
        var root = Path.Combine(Path.GetTempPath(), $"musoq-search-scope-validation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "one.txt"), "TODO\n");
        var opened = 0;
        try
        {
            using var hooks = SearchTestHooks.Install(new SearchTestHooksState
            {
                BeforeContentOpen = _ => Interlocked.Increment(ref opened)
            });

            var context = RuntimeV2TestContexts.CreateExecutionContext();
            var globalRules = new SearchScopeInput(
                globalIgnores: "disabled",
                globalIgnoreRules: ["*.tmp"]);
            var globalException = Assert.ThrowsException<SearchRequestException>(() =>
                new SearchPathsTypedSource(
                    root,
                    new SearchPathsOptionsInput(scope: globalRules),
                    context));
            Assert.AreEqual("scope.globalIgnoreRules", globalException.Diagnostic.Location?.ArgumentName);

            var configuredWithoutRules = new SearchScopeInput(globalIgnores: "configured");
            var configuredException = Assert.ThrowsException<SearchRequestException>(() =>
                new SearchPathsTypedSource(
                    root,
                    new SearchPathsOptionsInput(scope: configuredWithoutRules),
                    context));
            Assert.AreEqual("scope.globalIgnoreRules", configuredException.Diagnostic.Location?.ArgumentName);

            Assert.ThrowsException<SearchRequestException>(() =>
                new SearchPathsTypedSource(
                    root,
                    new SearchPathsOptionsInput(maxFiles: -1),
                    context));
            Assert.AreEqual(0, opened);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    [TestCategory("SearchContractFault")]
    public void TypedByteValidation_ShouldRejectJsonShapedAndMalformedHexInputs()
    {
        var root = Path.Combine(Path.GetTempPath(), $"musoq-search-byte-validation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        File.WriteAllBytes(Path.Combine(root, "one.bin"), [0x54, 0x4F]);
        try
        {
            var context = RuntimeV2TestContexts.CreateExecutionContext();
            foreach (var pattern in new[]
                     {
                         "{\"version\":1,\"bytes\":\"54\"}",
                         "5",
                         "0x54",
                         "GG"
                     })
            {
                var exception = Assert.ThrowsException<SearchPatternException>(() =>
                    SearchSqlHarness.MaterializeTyped(
                        new SearchBytesTypedSource(root, pattern, context).Chunks));
                Assert.AreEqual(SearchDiagnosticCodes.InvalidBytePattern, exception.Diagnostic.Code);
                Assert.AreEqual("patternHex", exception.Diagnostic.Location?.ArgumentName);
            }
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static void AssertDiagnostic(Action action, string location)
    {
        Exception? captured = null;
        try
        {
            action();
        }
        catch (Exception exception)
        {
            captured = exception;
        }

        Assert.IsNotNull(captured);
        Assert.IsInstanceOfType<ISearchDiagnosticException>(captured);
        var diagnostic = ((ISearchDiagnosticException)captured!).Diagnostic;
        Assert.AreEqual(location, diagnostic.Location?.ArgumentName);
    }
}
