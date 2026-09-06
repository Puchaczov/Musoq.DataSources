#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Search.Components.Contracts;

using Musoq.DataSources.Search.Components.Diagnostics;

using Musoq.DataSources.Search.Components.Text;

using Musoq.DataSources.Search.Components.Many;

using Musoq.DataSources.Search.Components.Traversal;

namespace Musoq.DataSources.Search.Tests.Components.Many;

[TestClass]
public sealed class SearchManyLiteralMatcherTests
{
    [TestMethod]
    public void Matcher_ShouldPreservePrefixMatchesAndEqualTextLabels()
    {
        var request = CreateRequest(
            ("short", "a"),
            ("long", "ab"),
            ("primary", "TODO"),
            ("secondary", "TODO"));

        var spans = Scan(request, "ab TODO TODO", 1, 2, 3, 1);

        CollectionAssert.AreEquivalent(
            new[]
            {
                (Id: "short", Start: 0L, Length: 1L),
                (Id: "long", Start: 0L, Length: 2L),
                (Id: "primary", Start: 3L, Length: 4L),
                (Id: "primary", Start: 8L, Length: 4L),
                (Id: "secondary", Start: 3L, Length: 4L),
                (Id: "secondary", Start: 8L, Length: 4L)
            },
            spans.Select(ToIdentity).ToArray());
    }

    [TestMethod]
    public void Matcher_ShouldKeepCrossPatternOverlapsButSelectEachPatternNonOverlapping()
    {
        var request = CreateRequest(
            ("whole", "aba"),
            ("suffix", "ba"),
            ("character", "a"));

        var spans = Scan(request, "ababa");

        CollectionAssert.AreEquivalent(
            new[]
            {
                (Id: "whole", Start: 0L, Length: 3L),
                (Id: "suffix", Start: 1L, Length: 2L),
                (Id: "suffix", Start: 3L, Length: 2L),
                (Id: "character", Start: 0L, Length: 1L),
                (Id: "character", Start: 2L, Length: 1L),
                (Id: "character", Start: 4L, Length: 1L)
            },
            spans.Select(ToIdentity).ToArray());

        Assert.AreEqual(1, spans.Count(span => span.PatternId == "whole"));
    }

    [TestMethod]
    public void Matcher_ShouldCarryLineCoordinatesAcrossChunkBoundaries()
    {
        var request = CreateRequest(("needle", "needle"));
        var spans = Scan(request, "first\nneedle\nlast", 2, 1, 3, 1);

        var span = spans.Single();
        Assert.AreEqual("needle", span.PatternId);
        Assert.AreEqual(6L, span.Start);
        Assert.AreEqual(2L, span.LineNumber);
        Assert.AreEqual(0L, span.Utf16Column);
        Assert.AreEqual(6L, span.Length);
    }

    [TestMethod]
    public void Matcher_ShouldCompileAndSearchTheBoundedMaximumPatternSet()
    {
        var patterns = Enumerable.Range(0, SearchManyRequestParser.MaxPatternCount)
            .Select(index => (Id: $"pattern-{index:D4}", Pattern: $"token-{index:D4}"))
            .ToArray();
        var request = CreateRequest(patterns);

        var spans = Scan(request, "token-0000 token-0512 token-1023", 5, 7, 11);

        CollectionAssert.AreEquivalent(
            new[] { "pattern-0000", "pattern-0512", "pattern-1023" },
            spans.Select(span => span.PatternId).ToArray());
    }

    [TestMethod]
    public void Matcher_ShouldApplyWholeWordIndependentlyToEveryLabel()
    {
        var request = CreateRequest(
            new SearchManyOptions(wholeWord: true),
            ("todo", "TODO"),
            ("do", "DO"));

        var spans = Scan(request, "TODO DO TODOX _TODO_");

        CollectionAssert.AreEquivalent(
            new[]
            {
                (Id: "todo", Start: 0L, Length: 4L),
                (Id: "do", Start: 5L, Length: 2L)
            },
            spans.Select(ToIdentity).ToArray());
    }

    [TestMethod]
    public void Scan_ShouldTraverseOnceAndOpenEachEligibleFileOnce()
    {
        var root = Path.Combine(Path.GetTempPath(), $"musoq-search-many-{Guid.NewGuid():N}");
        var counters = new SearchScopeCounters();
        var readerCalls = 0;
        var matches = new List<(string Path, string? Id, long Start)>();

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "first.txt"), "TODO TODO");
            File.WriteAllText(Path.Combine(root, "second.txt"), "nothing");
            File.WriteAllText(Path.Combine(root, "third.txt"), "TODO");

            SearchManyLiteralScan.ScanScope(
                root,
                CreateRequest(("todo", "TODO")),
                default,
                (path, span) => matches.Add((path, span.PatternId, span.Start)),
                path =>
                {
                    readerCalls++;
                    return new StringReader(File.ReadAllText(path));
                },
                counters);

            Assert.AreEqual(3L, counters.FilesConsidered);
            Assert.AreEqual(3L, counters.CandidatesYielded);
            Assert.AreEqual(3L, counters.ContentOpenAttempts);
            Assert.AreEqual(3, readerCalls);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    (Path: "first.txt", Id: (string?)"todo", Start: 0L),
                    (Path: "first.txt", Id: (string?)"todo", Start: 5L),
                    (Path: "third.txt", Id: (string?)"todo", Start: 0L)
                },
                matches);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Matcher_ShouldRejectNonLiteralPatternsBeforeAnyScopeRead()
    {
        var request = new SearchManyRequest(
        [
            new SearchManyPattern("regex", "TODO|FIXME", SearchPatternMode.Regex)
        ]);
        var readerOpened = false;

        var exception = Assert.ThrowsException<SearchRequestException>(
            () => SearchManyLiteralScan.ScanScope(
                Path.Combine(Path.GetTempPath(), "missing-search-many-root"),
                request,
                default,
                static (_, _) => { },
                _ =>
                {
                    readerOpened = true;
                    return new StringReader(string.Empty);
                }));

        StringAssert.Contains(exception.Diagnostic.Explanation, "not supported by the literal matcher");
        Assert.IsFalse(readerOpened);
    }

    private static SearchManyRequest CreateRequest(
        params (string Id, string Pattern)[] patterns)
    {
        return CreateRequest(SearchManyOptions.Default, patterns);
    }

    private static SearchManyRequest CreateRequest(
        SearchManyOptions options,
        params (string Id, string Pattern)[] patterns)
    {
        return new SearchManyRequest(
            patterns.Select(pattern => new SearchManyPattern(
                pattern.Id,
                pattern.Pattern,
                SearchPatternMode.Literal)),
            options);
    }

    private static MatchSpan[] Scan(
        SearchManyRequest request,
        string content,
        params int[] chunkSizes)
    {
        var matcher = new SearchManyLiteralMatcher(request);
        var spans = new List<MatchSpan>();
        var lineNumber = 1L;
        var utf16Column = 0L;
        var offset = 0;
        var chunkIndex = 0;

        while (offset < content.Length)
        {
            var requestedLength = chunkSizes.Length == 0
                ? content.Length - offset
                : chunkSizes[chunkIndex++ % chunkSizes.Length];
            var length = Math.Min(requestedLength, content.Length - offset);
            matcher.ConsumeBlock(
                content.AsSpan(offset, length),
                ref lineNumber,
                ref utf16Column,
                spans,
                ReadOnlySpan<SearchCharCoordinate>.Empty);
            offset += length;
        }

        matcher.Complete(spans);
        return spans.ToArray();
    }

    private static (string? Id, long Start, long Length) ToIdentity(MatchSpan span)
    {
        return (span.PatternId, span.Start, span.Length);
    }
}
