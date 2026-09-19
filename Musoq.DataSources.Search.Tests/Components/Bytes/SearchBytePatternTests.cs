#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Search.Components.Diagnostics;

using Musoq.DataSources.Search.Components.Bytes;

namespace Musoq.DataSources.Search.Tests.Components.Bytes;

[TestClass]
public sealed class SearchBytePatternTests
{
    [TestMethod]
    public void ParseHex_ShouldCompileExactBytesWithAnImplicitFullMask()
    {
        var pattern = SearchBytePatternParser.ParseHex("48 8b c0", mask: null);

        CollectionAssert.AreEqual(new byte[] { 0x48, 0x8B, 0xC0 }, pattern.Bytes.ToArray());
        CollectionAssert.AreEqual(new byte[] { 0xFF, 0xFF, 0xFF }, pattern.Masks.ToArray());
    }

    [TestMethod]
    public void ParseHex_ShouldCompileNibbleWildcardsIntoBitMasks()
    {
        var pattern = SearchBytePatternParser.ParseHex("4? ?f ??", mask: null);

        CollectionAssert.AreEqual(new byte[] { 0x40, 0x0F, 0x00 }, pattern.Bytes.ToArray());
        CollectionAssert.AreEqual(new byte[] { 0xF0, 0x0F, 0x00 }, pattern.Masks.ToArray());
    }

    [TestMethod]
    public void ParseHex_ShouldCompileAnExplicitNibbleMask()
    {
        var pattern = SearchBytePatternParser.ParseHex("4f 90", "f0 0f");

        CollectionAssert.AreEqual(new byte[] { 0x4F, 0x90 }, pattern.Bytes.ToArray());
        CollectionAssert.AreEqual(new byte[] { 0xF0, 0x0F }, pattern.Masks.ToArray());
    }

    [TestMethod]
    public void ParseHex_ShouldRejectOddHexDigitsWithAnActionableDiagnostic()
    {
        var exception = Assert.ThrowsException<SearchPatternException>(() =>
            SearchBytePatternParser.ParseHex("ABC", mask: null));

        Assert.AreEqual(SearchDiagnosticCodes.InvalidBytePattern, exception.Diagnostic.Code);
        Assert.AreEqual(SearchDiagnosticPhase.Argument, exception.Diagnostic.Phase);
        Assert.AreEqual("patternHex", exception.Diagnostic.Location?.ArgumentName);
        StringAssert.Contains(exception.Diagnostic.Explanation, "even number of hex nibbles");
    }

    [TestMethod]
    public void ParseHex_ShouldRejectInvalidBytesNumericLikePrefixesAndJsonShapedInput()
    {
        var invalid = Assert.ThrowsException<SearchPatternException>(() =>
            SearchBytePatternParser.ParseHex("GG", mask: null));
        StringAssert.Contains(invalid.Diagnostic.Explanation, "invalid hex nibble");

        var numericLike = Assert.ThrowsException<SearchPatternException>(() =>
            SearchBytePatternParser.ParseHex("0x90", mask: null));
        StringAssert.Contains(numericLike.Diagnostic.Explanation, "0x prefix is not accepted");

        var jsonShaped = Assert.ThrowsException<SearchPatternException>(() =>
            SearchBytePatternParser.ParseHex("{\"bytes\":\"90\"}", mask: null));
        StringAssert.Contains(jsonShaped.Diagnostic.Explanation, "invalid hex nibble");
    }

    [TestMethod]
    public void ParseHex_ShouldRejectEmptyPatternsAndPreserveAllWildcardMasks()
    {
        var empty = Assert.ThrowsException<SearchPatternException>(() =>
            SearchBytePatternParser.ParseHex("   ", mask: null));
        StringAssert.Contains(empty.Diagnostic.Explanation, "at least one byte value");

        var wildcardOnly = SearchBytePatternParser.ParseHex("??", mask: null);
        CollectionAssert.AreEqual(new byte[] { 0x00 }, wildcardOnly.Bytes.ToArray());
        CollectionAssert.AreEqual(new byte[] { 0x00 }, wildcardOnly.Masks.ToArray());
    }

    [TestMethod]
    public void ParseHex_ShouldRejectConflictingWildcardAndExplicitMaskOptions()
    {
        var exception = Assert.ThrowsException<SearchPatternException>(() =>
            SearchBytePatternParser.ParseHex("4? 90", "f0 ff"));

        StringAssert.Contains(exception.Diagnostic.Explanation, "cannot be combined");
        Assert.AreEqual("options.maskHex", exception.Diagnostic.Location?.ArgumentName);
    }

    [TestMethod]
    public void ParseHex_ShouldCompileBoundedWindowOptions()
    {
        var pattern = SearchBytePatternParser.ParseHex("90 91", null, 7, 11);

        Assert.IsTrue(pattern.Window.IsEnabled);
        Assert.AreEqual(7, pattern.Window.BeforeBytes);
        Assert.AreEqual(11, pattern.Window.AfterBytes);
    }

    [TestMethod]
    public void ParseHex_ShouldRejectMismatchedMasksAndOverBudgetWindows()
    {
        var mismatch = Assert.ThrowsException<SearchPatternException>(() =>
            SearchBytePatternParser.ParseHex("90 91", "ff", 0, 0));
        StringAssert.Contains(mismatch.Diagnostic.Explanation, "exactly 2 byte values");

        var overBudget = Assert.ThrowsException<SearchResourceLimitException>(() =>
            SearchBytePatternParser.ParseHex("90", null, 1_048_576, 0));
        Assert.AreEqual(SearchDiagnosticCodes.ResourceLimit, overBudget.Diagnostic.Code);
    }
}
