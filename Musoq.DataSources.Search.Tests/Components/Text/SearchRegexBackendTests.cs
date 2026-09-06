#nullable enable

using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Search.Components.Diagnostics;

using Musoq.DataSources.Search.Components.Text;

namespace Musoq.DataSources.Search.Tests.Components.Text;

[TestClass]
public sealed class SearchRegexBackendTests
{
    [TestInitialize]
    public void Initialize()
    {
        SearchRegexBackend.ResetForTests();
    }

    [TestCleanup]
    public void Cleanup()
    {
        SearchRegexBackend.ResetForTests();
    }

    [TestMethod]
    public void SupportedProfile_ShouldUseInvariantNonBacktrackingRegex()
    {
        var regex = SearchRegexBackend.Compile(
            "(?<word>[\\p{L}\\p{Nd}_]+)",
            SearchCaseMode.Insensitive);

        Assert.IsTrue(regex.Options.HasFlag(RegexOptions.NonBacktracking));
        Assert.IsTrue(regex.Options.HasFlag(RegexOptions.CultureInvariant));
        Assert.IsTrue(regex.Options.HasFlag(RegexOptions.IgnoreCase));
        Assert.AreEqual(SearchRegexBackend.MatchTimeout, regex.MatchTimeout);

        var match = regex.Match("prefix Żółw suffix");
        Assert.IsTrue(match.Success);
        Assert.AreEqual("prefix", match.Groups["word"].Value);
    }

    [TestMethod]
    public void SameStartAlternatives_ShouldUseRegexSourceOrder()
    {
        var shortFirst = SearchRegexBackend.Compile("a|ab");
        var longFirst = SearchRegexBackend.Compile("ab|a");

        Assert.AreEqual("a", shortFirst.Match("ab").Value);
        Assert.AreEqual("ab", longFirst.Match("ab").Value);
    }

    [TestMethod]
    public void InvalidSyntax_ShouldProducePortableDialectDiagnostic()
    {
        var exception = Assert.ThrowsException<SearchPatternException>(() =>
            SearchRegexBackend.Compile("["));

        Assert.AreEqual(SearchDiagnosticCodes.InvalidRegex, exception.Diagnostic.Code);
        Assert.AreEqual(SearchDiagnosticPhase.Syntax, exception.Diagnostic.Phase);
        StringAssert.Contains(exception.Diagnostic.Explanation, "portable non-backtracking");
        Assert.IsNotNull(exception.InnerException);
    }

    [DataTestMethod]
    [DataRow("(?=a)", "lookahead/lookaround")]
    [DataRow("(?<=a)", "lookbehind/lookaround")]
    [DataRow("(a)\\1", "backreferences")]
    [DataRow("\\k<name>", "backreferences")]
    [DataRow("\\Gabc", "contiguous-match anchors (\\G)")]
    [DataRow("(?>a)", "atomic groups")]
    [DataRow("(?(1)a|b)", "conditionals")]
    [DataRow("(?<open-close>a)", "balancing groups")]
    public void UnsupportedConstruct_ShouldNameDialectAndRemedy(
        string pattern,
        string construct)
    {
        var exception = Assert.ThrowsException<SearchPatternException>(() =>
            SearchRegexBackend.Compile(pattern));

        Assert.AreEqual(SearchDiagnosticCodes.InvalidRegex, exception.Diagnostic.Code);
        Assert.AreEqual("pattern", exception.Diagnostic.Location?.ArgumentName);
        StringAssert.Contains(exception.Diagnostic.Explanation, construct);
        StringAssert.Contains(exception.Diagnostic.SuggestedFix, construct);
        StringAssert.Contains(exception.Diagnostic.SuggestedFix, "literal mode");
        Assert.AreEqual(0, SearchRegexBackend.CompilationCount);
    }

    [TestMethod]
    public void OversizedPattern_ShouldFailBeforeCompilation()
    {
        var pattern = new string('a', SearchRegexBackend.MaxPatternLength + 1);

        var exception = Assert.ThrowsException<SearchResourceLimitException>(() =>
            SearchRegexBackend.Compile(pattern));

        Assert.AreEqual(SearchDiagnosticCodes.ResourceLimit, exception.Diagnostic.Code);
        Assert.AreEqual("pattern", exception.Diagnostic.Location?.ArgumentName);
        Assert.AreEqual(0, SearchRegexBackend.CompilationCount);
    }

    [TestMethod]
    public void ExcessiveNesting_ShouldFailBeforeRegexConstruction()
    {
        var depth = SearchRegexBackend.MaxNestingDepth + 1;
        var pattern = new string('(', depth) + "a" + new string(')', depth);

        var exception = Assert.ThrowsException<SearchResourceLimitException>(() =>
            SearchRegexBackend.Compile(pattern));

        Assert.AreEqual(SearchDiagnosticCodes.ResourceLimit, exception.Diagnostic.Code);
        Assert.IsTrue(exception.Diagnostic.Explanation.Contains("pattern nesting depth", StringComparison.Ordinal));
        Assert.AreEqual(0, SearchRegexBackend.CompilationCount);
    }

    [TestMethod]
    public void LargeAlternation_ShouldCompileWithinPatternBudget()
    {
        var pattern = string.Join(
            '|',
            Enumerable.Range(0, 4_096).Select(index => $"token-{index:0000}"));

        Assert.IsTrue(pattern.Length < SearchRegexBackend.MaxPatternLength);
        var regex = SearchRegexBackend.Compile(pattern);

        Assert.AreEqual("token-3072", regex.Match("prefix token-3072 suffix").Value);
        Assert.AreEqual(1, SearchRegexBackend.CompilationCount);
    }

    [TestMethod]
    public void RepeatedSemanticKey_ShouldCompileOnlyOnce()
    {
        var first = SearchRegexBackend.Compile("TODO");
        var second = SearchRegexBackend.Compile("TODO");
        var differentCase = SearchRegexBackend.Compile(
            "TODO",
            SearchCaseMode.Insensitive);

        Assert.AreSame(first, second);
        Assert.AreNotSame(first, differentCase);
        Assert.AreEqual(2, SearchRegexBackend.CompilationCount);
        Assert.AreEqual(2, SearchRegexBackend.CachedPatternCount);
    }

    [TestMethod]
    public void ConcurrentSameKeyRequests_ShouldShareOneCompiledRegex()
    {
        var regexes = new Regex[32];
        Parallel.For(
            0,
            regexes.Length,
            index => regexes[index] = SearchRegexBackend.Compile("TODO"));

        Assert.AreEqual(1, SearchRegexBackend.CompilationCount);
        Assert.IsTrue(regexes.All(regex => ReferenceEquals(regexes[0], regex)));
    }

    [TestMethod]
    public void CachePressure_ShouldRemainBoundedAndEvictLeastRecentPattern()
    {
        for (var index = 0; index < SearchRegexBackend.MaxCachedPatterns + 16; index++)
            _ = SearchRegexBackend.Compile($"pattern-{index}");

        Assert.AreEqual(SearchRegexBackend.MaxCachedPatterns, SearchRegexBackend.CachedPatternCount);
        Assert.AreEqual(SearchRegexBackend.MaxCachedPatterns + 16, SearchRegexBackend.CompilationCount);

        _ = SearchRegexBackend.Compile("pattern-0");

        Assert.AreEqual(SearchRegexBackend.MaxCachedPatterns + 17, SearchRegexBackend.CompilationCount);
        Assert.AreEqual(SearchRegexBackend.MaxCachedPatterns, SearchRegexBackend.CachedPatternCount);
    }

    [TestMethod]
    public void CompiledCache_ShouldReleaseEvictedAndResetEntries()
    {
        var evicted = SearchRegexBackend.Compile("pattern-0");
        for (var index = 1; index <= SearchRegexBackend.MaxCachedPatterns; index++)
            _ = SearchRegexBackend.Compile($"pattern-{index}");

        var recompiled = SearchRegexBackend.Compile("pattern-0");

        Assert.AreNotSame(evicted, recompiled);
        Assert.AreEqual(SearchRegexBackend.MaxCachedPatterns, SearchRegexBackend.CachedPatternCount);

        SearchRegexBackend.ResetForTests();

        Assert.AreEqual(0, SearchRegexBackend.CachedPatternCount);
        Assert.AreEqual(0, SearchRegexBackend.CompilationCount);

        var fresh = SearchRegexBackend.Compile("pattern-0");
        Assert.AreNotSame(recompiled, fresh);
    }
}
