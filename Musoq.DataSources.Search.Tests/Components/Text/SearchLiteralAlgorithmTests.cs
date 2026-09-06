#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Musoq.DataSources.Search.Components.Contracts;

using Musoq.DataSources.Search.Components.Text;

namespace Musoq.DataSources.Search.Tests.Components.Text;

[TestClass]
public sealed class SearchLiteralAlgorithmTests
{
    [TestMethod]
    public void SingleCharacterIndexOfPath_ShouldMatchScalarPathForUnalignedNearEndBlocks()
    {
        const string content = "xT ordinary 😀\nTT at the end TzT";
        const int start = 1;
        var length = content.Length - 2;
        var chunkSizes = new[] { 1, 4, 2, 7, 1, 3, 5 };

        var optimized = Scan(
            content,
            start,
            length,
            chunkSizes,
            useSingleCharacterIndexOf: true);
        var scalar = Scan(
            content,
            start,
            length,
            chunkSizes,
            useSingleCharacterIndexOf: false);

        Assert.AreEqual(
            string.Join('|', scalar.Select(Signature)),
            string.Join('|', optimized.Select(Signature)));
        CollectionAssert.AreEqual(
            new[] { "0:1:0", "14:2:0", "15:2:1", "28:2:14" },
            optimized.Select(Signature).ToArray());
    }

    [TestMethod]
    public void LiteralAlgorithms_ShouldRemainEquivalentForArchitectureIndependentBoundaryMatrix()
    {
        var inputs = new[]
        {
            (Content: "T", Literal: "T"),
            (Content: "ordinary text without a marker", Literal: "T"),
            (Content: "prefix T suffix T", Literal: "T"),
            (Content: "😀T😀\nT", Literal: "T"),
            (Content: "TTTTT", Literal: "T")
        };

        foreach (var input in inputs)
        {
            var optimized = Scan(
                input.Content,
                0,
                input.Content.Length,
                [1, 2, 3, 5],
                useSingleCharacterIndexOf: true,
                literal: input.Literal);
            var scalar = Scan(
                input.Content,
                0,
                input.Content.Length,
                [1, 2, 3, 5],
                useSingleCharacterIndexOf: false,
                literal: input.Literal);

            CollectionAssert.AreEqual(
                scalar.Select(Signature).ToArray(),
                optimized.Select(Signature).ToArray(),
                input.Content);
        }
    }

    private static IReadOnlyList<MatchSpan> Scan(
        string content,
        int start,
        int length,
        IReadOnlyList<int> chunkSizes,
        bool useSingleCharacterIndexOf,
        string literal = "T")
    {
        var matcher = new LiteralMatcher(
            literal,
            SearchCaseMode.Sensitive,
            wholeWord: false,
            useSingleCharacterIndexOf: useSingleCharacterIndexOf);
        var spans = new List<MatchSpan>();
        var lineNumber = 1L;
        var utf16Column = 0L;
        var position = 0;
        var chunkIndex = 0;
        while (position < length)
        {
            var requestedLength = chunkSizes[Math.Min(chunkIndex++, chunkSizes.Count - 1)];
            var chunkLength = Math.Min(requestedLength, length - position);
            matcher.ConsumeBlock(
                content.AsSpan(start + position, chunkLength),
                ref lineNumber,
                ref utf16Column,
                spans);
            position += chunkLength;
        }

        matcher.Complete(spans);
        return spans;
    }

    private static string Signature(MatchSpan span)
    {
        return string.Join(':', span.Start, span.LineNumber, span.Utf16Column);
    }
}
