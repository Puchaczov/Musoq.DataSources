#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Search.Components.Diagnostics;

using Musoq.DataSources.Search.Components.Bytes;

namespace Musoq.DataSources.Search.Tests.Components.Bytes;

[TestClass]
public sealed class SearchBytePatternTests
{
    [TestMethod]
    public void Parse_ShouldCompileExactBytesWithAnImplicitFullMask()
    {
        var pattern = SearchBytePatternParser.Parse(
            "{\"version\":1,\"bytes\":\"48 8b c0\"}");

        CollectionAssert.AreEqual(new byte[] { 0x48, 0x8B, 0xC0 }, pattern.Bytes.ToArray());
        CollectionAssert.AreEqual(new byte[] { 0xFF, 0xFF, 0xFF }, pattern.Masks.ToArray());
    }

    [TestMethod]
    public void Parse_ShouldCompileNibbleWildcardsIntoBitMasks()
    {
        var pattern = SearchBytePatternParser.Parse(
            "{\"version\":1,\"bytes\":\"4? ?f ??\"}");

        CollectionAssert.AreEqual(new byte[] { 0x40, 0x0F, 0x00 }, pattern.Bytes.ToArray());
        CollectionAssert.AreEqual(new byte[] { 0xF0, 0x0F, 0x00 }, pattern.Masks.ToArray());
    }

    [TestMethod]
    public void Parse_ShouldCompileAnExplicitNibbleMask()
    {
        var pattern = SearchBytePatternParser.Parse(
            "{\"version\":1,\"bytes\":\"4f 90\",\"mask\":\"f0 0f\"}");

        CollectionAssert.AreEqual(new byte[] { 0x4F, 0x90 }, pattern.Bytes.ToArray());
        CollectionAssert.AreEqual(new byte[] { 0xF0, 0x0F }, pattern.Masks.ToArray());
    }

    [TestMethod]
    public void Parse_ShouldRejectOddHexDigitsWithAnActionableDiagnostic()
    {
        var exception = Assert.ThrowsException<SearchPatternException>(() =>
            SearchBytePatternParser.Parse("{\"version\":1,\"bytes\":\"ABC\"}"));

        Assert.AreEqual(SearchDiagnosticCodes.InvalidBytePattern, exception.Diagnostic.Code);
        Assert.AreEqual(SearchDiagnosticPhase.Argument, exception.Diagnostic.Phase);
        Assert.AreEqual("pattern", exception.Diagnostic.Location?.ArgumentName);
        StringAssert.Contains(exception.Diagnostic.Explanation, "even number of hex nibbles");
        StringAssert.Contains(exception.Diagnostic.SuggestedFix, "explicit even-length hex");
    }

    [TestMethod]
    public void Parse_ShouldRejectInvalidBytesAndNumericLikePrefixes()
    {
        var invalid = Assert.ThrowsException<SearchPatternException>(() =>
            SearchBytePatternParser.Parse("{\"version\":1,\"bytes\":\"GG\"}"));
        StringAssert.Contains(invalid.Diagnostic.Explanation, "invalid hex nibble");

        var numericLike = Assert.ThrowsException<SearchPatternException>(() =>
            SearchBytePatternParser.Parse("{\"version\":1,\"bytes\":\"0x90\"}"));
        StringAssert.Contains(numericLike.Diagnostic.Explanation, "0x prefix is not accepted");

        var numeric = Assert.ThrowsException<SearchPatternException>(() =>
            SearchBytePatternParser.Parse("{\"version\":1,\"bytes\":144}"));
        StringAssert.Contains(numeric.Diagnostic.Explanation, "'bytes' must be a string");
    }

    [TestMethod]
    public void Parse_ShouldRejectEmptyPatternsAndPreserveAllWildcardMasks()
    {
        var empty = Assert.ThrowsException<SearchPatternException>(() =>
            SearchBytePatternParser.Parse("{\"version\":1,\"bytes\":\"   \"}"));
        StringAssert.Contains(empty.Diagnostic.Explanation, "at least one byte value");

        var wildcardOnly = SearchBytePatternParser.Parse(
            "{\"version\":1,\"bytes\":\"??\"}");
        CollectionAssert.AreEqual(new byte[] { 0x00 }, wildcardOnly.Bytes.ToArray());
        CollectionAssert.AreEqual(new byte[] { 0x00 }, wildcardOnly.Masks.ToArray());
    }

    [TestMethod]
    public void Parse_ShouldRejectConflictingWildcardAndExplicitMaskOptions()
    {
        var exception = Assert.ThrowsException<SearchPatternException>(() =>
            SearchBytePatternParser.Parse(
                "{\"version\":1,\"bytes\":\"4? 90\",\"mask\":\"f0 ff\"}"));

        StringAssert.Contains(exception.Diagnostic.Explanation, "cannot be combined");
        Assert.AreEqual("pattern", exception.Diagnostic.Location?.ArgumentName);
    }

    [TestMethod]
    public void Parse_ShouldRejectConflictingEndiannessOption()
    {
        var exception = Assert.ThrowsException<SearchPatternException>(() =>
            SearchBytePatternParser.Parse(
                "{\"version\":1,\"bytes\":\"90\",\"endianness\":\"little\"}"));

        StringAssert.Contains(exception.Diagnostic.Explanation, "byte-order neutral");
        StringAssert.Contains(exception.Diagnostic.SuggestedFix, "SQL numeric literal");
    }

    [TestMethod]
    public void Parse_ShouldRejectMissingOrUnsupportedVersionAndDuplicateProperties()
    {
        var missing = Assert.ThrowsException<SearchPatternException>(() =>
            SearchBytePatternParser.Parse("{\"bytes\":\"90\"}"));
        StringAssert.Contains(missing.Diagnostic.Explanation, "'version' is required");

        var unsupported = Assert.ThrowsException<SearchPatternException>(() =>
            SearchBytePatternParser.Parse("{\"version\":2,\"bytes\":\"90\"}"));
        StringAssert.Contains(unsupported.Diagnostic.Explanation, "'version' must be 1");

        var duplicate = Assert.ThrowsException<SearchPatternException>(() =>
            SearchBytePatternParser.Parse("{\"version\":1,\"bytes\":\"90\",\"bytes\":\"91\"}"));
        StringAssert.Contains(duplicate.Diagnostic.Explanation, "duplicate JSON property");
    }

    [TestMethod]
    public void Parse_ShouldCompileBoundedWindowOptions()
    {
        var pattern = SearchBytePatternParser.Parse(
            "{\"version\":1,\"bytes\":\"90 91\",\"window\":{\"beforeBytes\":7,\"afterBytes\":11}}");

        Assert.IsTrue(pattern.Window.IsEnabled);
        Assert.AreEqual(7, pattern.Window.BeforeBytes);
        Assert.AreEqual(11, pattern.Window.AfterBytes);
    }

    [TestMethod]
    public void Parse_ShouldRejectUnknownOrOverBudgetWindowOptions()
    {
        var unknown = Assert.ThrowsException<SearchPatternException>(() =>
            SearchBytePatternParser.Parse(
                "{\"version\":1,\"bytes\":\"90\",\"window\":{\"before\":1}}"));
        StringAssert.Contains(unknown.Diagnostic.Explanation, "unknown window property");

        var overBudget = Assert.ThrowsException<SearchResourceLimitException>(() =>
            SearchBytePatternParser.Parse(
                "{\"version\":1,\"bytes\":\"90\",\"window\":{\"beforeBytes\":1048576}}"));
        Assert.AreEqual(SearchDiagnosticCodes.ResourceLimit, overBudget.Diagnostic.Code);
    }
}
